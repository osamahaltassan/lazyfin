using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;

/// <summary>
/// The server's <see cref="ISubtitleEncoder"/>, with <see cref="RtlLineRule"/> applied to the subtitles it hands to clients.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetSubtitles"/> fixes the output only when the source stream is a text subtitle that is not ASS or SSA,
/// and the output is JSON or SRT. Every other subtitle comes back as the server wrote it. So does one the fix cannot
/// read, such as one in a legacy encoding, so the fix never stops a subtitle from loading.
/// </para>
/// <para>
/// The other members pass straight through. <see cref="GetSubtitleFilePath"/> must, because burn-in of embedded
/// tracks and, from 12.3, image subtitle delivery read the file it names.
/// </para>
/// <para>
/// <c>AddRtlFix</c> builds it around the server's own encoder. The container owns that encoder, so this never disposes it.
/// </para>
/// </remarks>
internal sealed class RtlFixSubtitleEncoder : ISubtitleEncoder
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ISubtitleEncoder _inner;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILogger<RtlFixSubtitleEncoder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtlFixSubtitleEncoder"/> class.
    /// </summary>
    /// <param name="inner">The encoder this one wraps: the server's own, or another plugin's decorator around it.</param>
    /// <param name="mediaSourceManager">The server's media sources, to find the source subtitle stream.</param>
    /// <param name="logger">Where it says why a subtitle came back unchanged.</param>
    public RtlFixSubtitleEncoder(ISubtitleEncoder inner, IMediaSourceManager mediaSourceManager, ILogger<RtlFixSubtitleEncoder> logger)
    {
        _inner = inner;
        _mediaSourceManager = mediaSourceManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Stream> GetSubtitles(BaseItem item, string mediaSourceId, int subtitleStreamIndex, string outputFormat, long startTimeTicks, long endTimeTicks, bool preserveOriginalTimestamps, CancellationToken cancellationToken)
    {
        var output = await _inner.GetSubtitles(item, mediaSourceId, subtitleStreamIndex, outputFormat, startTimeTicks, endTimeTicks, preserveOriginalTimestamps, cancellationToken).ConfigureAwait(false);
        if (RuleFormatOf(outputFormat) is not { } format || !IsFixableSource(item, mediaSourceId, subtitleStreamIndex))
        {
            return output;
        }

        byte[] bytes;
        await using (output.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await output.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            bytes = buffer.ToArray();
        }

        return new MemoryStream(Fix(bytes, format, mediaSourceId, subtitleStreamIndex));
    }

    /// <inheritdoc />
    public Task<string> GetSubtitleFileCharacterSet(MediaStream subtitleStream, string language, MediaSourceInfo mediaSource, CancellationToken cancellationToken)
        => _inner.GetSubtitleFileCharacterSet(subtitleStream, language, mediaSource, cancellationToken);

    /// <inheritdoc />
    public Task<string> GetSubtitleFilePath(MediaStream subtitleStream, MediaSourceInfo mediaSource, CancellationToken cancellationToken)
        => _inner.GetSubtitleFilePath(subtitleStream, mediaSource, cancellationToken);

    /// <inheritdoc />
    public Task ExtractAllExtractableSubtitles(MediaSourceInfo mediaSource, CancellationToken cancellationToken)
        => _inner.ExtractAllExtractableSubtitles(mediaSource, cancellationToken);

    /// <summary>
    /// Maps an output format the server writes to the format the rule reads, or <c>null</c> for one this fix leaves alone.
    /// </summary>
    private static SubtitleTextFormat? RuleFormatOf(string outputFormat) => outputFormat?.ToLowerInvariant() switch
    {
        "json" => SubtitleTextFormat.Json,
        SubtitleFormat.SRT or SubtitleFormat.SUBRIP => SubtitleTextFormat.SubRip,
        _ => null,
    };

    /// <summary>
    /// Tells whether the source subtitle stream is one the RTL fix changes: a text subtitle that is not ASS or SSA.
    /// </summary>
    /// <remarks>
    /// The source codec decides, not the output format, because the server also converts ASS to other formats.
    /// </remarks>
    private bool IsFixableSource(BaseItem item, string mediaSourceId, int subtitleStreamIndex)
    {
        MediaStream? source;
        try
        {
            source = _mediaSourceManager.GetStaticMediaSources(item, false)
                .FirstOrDefault(s => string.Equals(s.Id, mediaSourceId, StringComparison.OrdinalIgnoreCase))
                ?.MediaStreams
                ?.FirstOrDefault(s => s.Type == MediaStreamType.Subtitle && s.Index == subtitleStreamIndex);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RTL fix could not look up subtitle stream {Index} of media source {MediaSourceId}", subtitleStreamIndex, mediaSourceId);
            return false;
        }

        if (source is null)
        {
            _logger.LogDebug("RTL fix found no subtitle stream {Index} in media source {MediaSourceId}", subtitleStreamIndex, mediaSourceId);
            return false;
        }

        return source.IsTextSubtitleStream
            && !string.Equals(source.Codec, SubtitleFormat.ASS, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(source.Codec, SubtitleFormat.SSA, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies the rule to UTF-8 subtitle bytes. A byte order mark stays, because the rule keeps it.
    /// </summary>
    /// <returns>The fixed bytes, or <paramref name="bytes"/> itself when they are not UTF-8 or the rule throws.</returns>
    private byte[] Fix(byte[] bytes, SubtitleTextFormat format, string mediaSourceId, int subtitleStreamIndex)
    {
        try
        {
            var text = _strictUtf8.GetString(bytes);
            var fixedText = RtlLineRule.Apply(text, format);
            return ReferenceEquals(fixedText, text) ? bytes : _strictUtf8.GetBytes(fixedText);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RTL fix left subtitle stream {Index} of media source {MediaSourceId} unchanged", subtitleStreamIndex, mediaSourceId);
            return bytes;
        }
    }
}
