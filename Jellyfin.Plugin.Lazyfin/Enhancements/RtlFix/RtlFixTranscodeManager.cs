using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;

/// <summary>
/// The server's <see cref="ITranscodeManager"/>, with burn-in pointed at a copy of the subtitle that has
/// <see cref="RtlLineRule"/> applied.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StartFfMpeg"/> acts only when the transcode burns in a text subtitle that is not ASS or SSA, from an
/// <c>.srt</c>, <c>.vtt</c>, or <c>.ttml</c> file. It writes the fixed copy as UTF-8 into <c>lazyfin/rtl-fix</c> inside
/// the server's cache path, and points ffmpeg's <c>subtitles</c> filter at it. Jellyfin's "Clean Cache Directory" task
/// deletes a copy no transcode used for 30 days.
/// </para>
/// <para>
/// When it cannot fix a burn-in, such as a subtitle in an encoding .NET does not know, it logs a warning and starts
/// ffmpeg with the server's own command line, so the fix never stops a transcode. A subtitle with no RTL line also
/// burns in from the original.
/// </para>
/// <para>
/// The other members pass straight through. <c>AddRtlFix</c> builds it around the server's own manager. The container
/// owns that manager, so this never disposes it.
/// </para>
/// </remarks>
internal sealed class RtlFixTranscodeManager : ITranscodeManager
{
    private const string CharsetOption = ":charenc=";

    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ITranscodeManager _inner;
    private readonly ISubtitleEncoder _subtitleEncoder;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<RtlFixTranscodeManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtlFixTranscodeManager"/> class.
    /// </summary>
    /// <param name="inner">The manager this one wraps: the server's own, or another plugin's decorator around it.</param>
    /// <param name="subtitleEncoder">The server's subtitles, to find the file burn-in reads for a track inside the video.</param>
    /// <param name="mediaEncoder">The server's ffmpeg, to escape paths the way the burn-in filter has them.</param>
    /// <param name="applicationPaths">The server's folders, to keep the fixed copies in its cache.</param>
    /// <param name="logger">Where it says why a burn-in was left unchanged.</param>
    public RtlFixTranscodeManager(
        ITranscodeManager inner,
        ISubtitleEncoder subtitleEncoder,
        IMediaEncoder mediaEncoder,
        IApplicationPaths applicationPaths,
        ILogger<RtlFixTranscodeManager> logger)
    {
        _inner = inner;
        _subtitleEncoder = subtitleEncoder;
        _mediaEncoder = mediaEncoder;
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <inheritdoc />
    public TranscodingJob? GetTranscodingJob(string playSessionId)
        => _inner.GetTranscodingJob(playSessionId);

    /// <inheritdoc />
    public TranscodingJob? GetTranscodingJob(string path, TranscodingJobType type)
        => _inner.GetTranscodingJob(path, type);

    /// <inheritdoc />
    public void PingTranscodingJob(string playSessionId, bool? isUserPaused)
        => _inner.PingTranscodingJob(playSessionId, isUserPaused);

    /// <inheritdoc />
    public Task KillTranscodingJobs(string deviceId, string? playSessionId, Func<string, bool> deleteFiles)
        => _inner.KillTranscodingJobs(deviceId, playSessionId, deleteFiles);

    /// <inheritdoc />
    public void ReportTranscodingProgress(TranscodingJob job, StreamState state, TimeSpan? transcodingPosition, float? framerate, double? percentComplete, long? bytesTranscoded, int? bitRate)
        => _inner.ReportTranscodingProgress(job, state, transcodingPosition, framerate, percentComplete, bytesTranscoded, bitRate);

    /// <inheritdoc />
    public async Task<TranscodingJob> StartFfMpeg(StreamState state, string outputPath, string commandLineArguments, Guid userId, TranscodingJobType transcodingJobType, CancellationTokenSource cancellationTokenSource, string? workingDirectory = null)
    {
        if (BurnsInFixableSubtitle(state))
        {
            commandLineArguments = await PointBurnInAtFixedCopy(state, commandLineArguments).ConfigureAwait(false);
        }

        return await _inner.StartFfMpeg(state, outputPath, commandLineArguments, userId, transcodingJobType, cancellationTokenSource, workingDirectory).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public TranscodingJob? OnTranscodeBeginRequest(string path, TranscodingJobType type)
        => _inner.OnTranscodeBeginRequest(path, type);

    /// <inheritdoc />
    public void OnTranscodeEndRequest(TranscodingJob job)
        => _inner.OnTranscodeEndRequest(job);

    /// <inheritdoc />
    public ValueTask<IDisposable> LockAsync(string outputPath, CancellationToken cancellationToken)
        => _inner.LockAsync(outputPath, cancellationToken);

    /// <summary>
    /// Tells whether the transcode draws a subtitle the RTL fix changes into the picture: a text subtitle that is not ASS or SSA.
    /// </summary>
    private static bool BurnsInFixableSubtitle(StreamState state)
        => state.SubtitleDeliveryMethod == SubtitleDeliveryMethod.Encode
            && state.SubtitleStream is { IsTextSubtitleStream: true } subtitle
            && !string.Equals(subtitle.Codec, SubtitleFormat.ASS, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(subtitle.Codec, SubtitleFormat.SSA, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a subtitle file's extension to the format the rule reads, or <c>null</c> for one this fix leaves alone.
    /// </summary>
    private static SubtitleTextFormat? RuleFormatOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".srt" => SubtitleTextFormat.SubRip,
        ".vtt" => SubtitleTextFormat.WebVtt,
        ".ttml" => SubtitleTextFormat.Ttml,
        _ => null,
    };

    /// <summary>
    /// Reads the charset name that follows <c>:charenc=</c> at <paramref name="start"/>.
    /// </summary>
    /// <returns>The index right after the name.</returns>
    private static int EndOfCharset(string commandLine, int start)
    {
        var end = start;
        while (end < commandLine.Length && (char.IsAsciiLetterOrDigit(commandLine[end]) || commandLine[end] is '-' or '_' or '.'))
        {
            end++;
        }

        return end;
    }

    /// <summary>
    /// Finds an encoding by the name ffmpeg's <c>:charenc=</c> option has, with invalid bytes made an error.
    /// </summary>
    /// <exception cref="ArgumentException">.NET knows no encoding by that name.</exception>
    private static Encoding EncodingOf(string? charset)
        => string.IsNullOrEmpty(charset)
            ? _utf8
            : CodePagesEncodingProvider.Instance.GetEncoding(charset, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                ?? Encoding.GetEncoding(charset, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    /// <summary>
    /// Names the copy after the source file as it is now, and the charset it is read with, so a changed source gets a new copy.
    /// </summary>
    private static string CopyNameOf(FileInfo source, string? charset, string extension)
    {
        var key = string.Join('\n', source.FullName, source.Length.ToString(CultureInfo.InvariantCulture), source.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture), charset ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + extension;
    }

    /// <summary>
    /// Rewrites the burn-in filter, <c>subtitles=f='&lt;source&gt;'</c> and any <c>:charenc=</c> right after it, to
    /// <c>subtitles=f='&lt;fixed copy&gt;'</c>, and keeps every other option.
    /// </summary>
    /// <returns>The new command line, or <paramref name="commandLine"/> itself when it leaves the burn-in unchanged.</returns>
    private async Task<string> PointBurnInAtFixedCopy(StreamState state, string commandLine)
    {
        string? sourcePath = null;
        try
        {
            sourcePath = state.SubtitleStream.IsExternal
                ? state.SubtitleStream.Path
                : await _subtitleEncoder.GetSubtitleFilePath(state.SubtitleStream, state.MediaSource, CancellationToken.None).ConfigureAwait(false);
            if (RuleFormatOf(sourcePath) is not { } format)
            {
                return commandLine;
            }

            var filter = $"subtitles=f='{_mediaEncoder.EscapeSubtitleFilterPath(sourcePath)}'";
            var start = commandLine.IndexOf(filter, StringComparison.Ordinal);
            if (start < 0)
            {
                _logger.LogWarning("RTL fix found no burn-in filter for {Path} in the ffmpeg command line, so it burns in unchanged", sourcePath);
                return commandLine;
            }

            var end = start + filter.Length;
            string? charset = null;
            if (string.CompareOrdinal(commandLine, end, CharsetOption, 0, CharsetOption.Length) == 0)
            {
                var charsetEnd = EndOfCharset(commandLine, end + CharsetOption.Length);
                charset = commandLine[(end + CharsetOption.Length)..charsetEnd];
                end = charsetEnd;
            }

            if (await GetFixedCopy(sourcePath, charset, format).ConfigureAwait(false) is not { } copyPath)
            {
                return commandLine;
            }

            return string.Concat(commandLine.AsSpan(0, start), $"subtitles=f='{_mediaEncoder.EscapeSubtitleFilterPath(copyPath)}'", commandLine.AsSpan(end));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RTL fix could not prepare subtitle {Path} for burn-in, so it burns in unchanged", sourcePath);
            return commandLine;
        }
    }

    /// <summary>
    /// Finds or writes the fixed UTF-8 copy of a subtitle file. A copy already written for the same source is reused, and
    /// its last write time is renewed so the cache cleanup keeps it while it is in use.
    /// </summary>
    /// <returns>The copy's path, or <c>null</c> when the subtitle has no RTL line.</returns>
    /// <exception cref="ArgumentException">.NET knows no encoding named <paramref name="charset"/>.</exception>
    /// <exception cref="DecoderFallbackException">The file is not valid in that encoding.</exception>
    private async Task<string?> GetFixedCopy(string sourcePath, string? charset, SubtitleTextFormat format)
    {
        var source = new FileInfo(sourcePath);
        var folder = Path.Combine(_applicationPaths.CachePath, "lazyfin", "rtl-fix");
        var copyPath = Path.Combine(folder, CopyNameOf(source, charset, source.Extension.ToLowerInvariant()));
        if (File.Exists(copyPath))
        {
            File.SetLastWriteTimeUtc(copyPath, DateTime.UtcNow);
            return copyPath;
        }

        var text = EncodingOf(charset).GetString(await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false));
        var fixedText = RtlLineRule.Apply(text, format);
        if (ReferenceEquals(fixedText, text))
        {
            return null;
        }

        Directory.CreateDirectory(folder);
        var partialPath = $"{copyPath}.{Guid.NewGuid():N}.partial";
        await File.WriteAllBytesAsync(partialPath, _utf8.GetBytes(fixedText.TrimStart('﻿'))).ConfigureAwait(false);
        File.Move(partialPath, copyPath, overwrite: true);
        return copyPath;
    }
}
