using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.Lazyfin.Tests.Enhancements.RtlFix;

/// <summary>
/// Checks what Jellyfin's transcodes see after <c>AddRtlFix</c>: the transcode manager it resolves, and the ffmpeg command
/// line that manager receives when a transcode burns in a subtitle.
/// </summary>
/// <remarks>
/// Each test registers a fake server <see cref="ITranscodeManager"/> the way Jellyfin's <c>ApplicationHost</c> does, and
/// calls it the way Jellyfin's HLS and progressive stream controllers do. The subtitle files and the server's cache live
/// in a temporary folder that each test deletes.
/// </remarks>
public sealed partial class AddRtlFixTests : IDisposable
{
    private const string OutputPath = "/cache/transcodes/32946f5d9119cadf5a62b4488ea784ba.m3u8";

    private const string FontsFolder = "/data/attachments/b2/b21266e0-01e4-2ac8-6c4a-7a9f07508bca";

    private static readonly Guid UserId = Guid.Parse("0d9a5f0e4c1b4f3a8e2d7c6b5a493827");

    /// <summary>
    /// An Arabic SRT cue saved in Windows-1256: مرحبا. on one line.
    /// </summary>
    private static readonly byte[] Windows1256ArabicSrt =
        [.. "1\r\n00:00:01,000 --> 00:00:03,000\r\n"u8, 0xE3, 0xD1, 0xCD, 0xC8, 0xC7, (byte)'.', .. "\r\n\r\n"u8];

    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("lazyfin-rtl-fix-");

    private readonly Mock<IMediaEncoder> _mediaEncoder = new();

    private readonly Mock<IApplicationPaths> _paths = new();

    private readonly ServerTranscodes _transcodes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AddRtlFixTests"/> class, with a server cache in a new temporary folder.
    /// </summary>
    public AddRtlFixTests()
    {
        _mediaEncoder.Setup(e => e.EscapeSubtitleFilterPath(It.IsAny<string>())).Returns<string>(EscapeSubtitleFilterPath);
        _paths.Setup(p => p.CachePath).Returns(CachePath);
    }

    private string CachePath => Path.Combine(_temp.FullName, "cache");

    private string MediaFolder => Path.Combine(_temp.FullName, "media", "Lazyfin's Test (2026)");

    private string VideoPath => Path.Combine(MediaFolder, "Lazyfin Test (2026).mkv");

    [Fact]
    public void TranscodeManager_IsTheRtlFixAroundOneServerManager()
    {
        using (var provider = BuildServices(services => services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>()))
        {
            for (var i = 0; i < 5; i++)
            {
                Assert.IsNotType<FakeTranscodeManager>(provider.GetRequiredService<ITranscodeManager>());
                Assert.IsNotType<FakeSubtitleEncoder>(provider.GetRequiredService<ISubtitleEncoder>());
            }

            Assert.Same(provider.GetRequiredService<ITranscodeManager>(), provider.GetRequiredService<ITranscodeManager>());
            Assert.Equal(1, _transcodes.ManagersBuilt);
            Assert.Equal(1, _server.EncodersBuilt);
            Assert.Equal(0, _transcodes.ManagersDisposed);
        }

        Assert.Equal(1, _transcodes.ManagersDisposed);
    }

    [Fact]
    public async Task TranscodeDecoratorAnotherPluginRegisteredFirst_StaysInTheChain()
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", Encoding.UTF8.GetBytes(ArabicSrt));
        using var state = BurnIn(SubtitleFileNextToTheVideo(source));

        var manager = ResolveTranscodeManager(services =>
        {
            services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>();
            services.RemoveAll<ITranscodeManager>();
            services.AddSingleton<FakeTranscodeManager>();
            services.AddSingleton<ITranscodeManager>(sp => new OtherPluginTranscodeManager(sp.GetRequiredService<FakeTranscodeManager>(), _transcodes));
        });
        var commandLine = await StartFfMpegAsync(manager, state, CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}'"));

        var copy = Assert.Single(FixedCopies());
        Assert.Equal(CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(copy)}'"), commandLine);
        Assert.Equal(["other plugin", $"start {OutputPath} {UserId:N} Hls "], _transcodes.Requests);
        Assert.Equal(1, _transcodes.ManagersBuilt);
    }

    [Fact]
    public async Task ArabicSrtFileNextToTheVideo_BurnsInFromAFixedUtf8Copy()
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", Encoding.UTF8.GetBytes(ArabicSrt));
        using var state = BurnIn(SubtitleFileNextToTheVideo(source));
        var manager = ResolveTranscodeManager();

        var job = await manager.StartFfMpeg(state, OutputPath, CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}':fontsdir='{FontsFolder}'"), UserId, TranscodingJobType.Hls, new CancellationTokenSource());

        var copy = Assert.Single(FixedCopies());
        Assert.Equal(CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(copy)}':fontsdir='{FontsFolder}'"), Assert.Single(_transcodes.CommandLines));
        Assert.Equal(".srt", Path.GetExtension(copy));
        Assert.Equal(Encoding.UTF8.GetBytes(WrappedArabicSrt), await File.ReadAllBytesAsync(copy, TestContext.Current.CancellationToken));
        Assert.Equal(Encoding.UTF8.GetBytes(ArabicSrt), await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));
        Assert.Same(_transcodes.Job, job);
    }

    [Fact]
    public async Task ArabicSrtTrackInsideTheVideo_BurnsInFromAFixedUtf8Copy()
    {
        _server.ExtractedPath = WriteSubtitle($"{MediaSourceId}.srt", Encoding.UTF8.GetBytes(ArabicSrt));
        using var state = BurnIn(new MediaStream { Type = MediaStreamType.Subtitle, Index = SubtitleIndex, Codec = "subrip", IsExternal = false, Path = VideoPath });
        var options = $":alpha=1:sub2video=1:fontsdir='{FontsFolder}'";

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(_server.ExtractedPath)}'{options}"));

        var copy = Assert.Single(FixedCopies());
        Assert.Equal(CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(copy)}'{options}"), commandLine);
        Assert.Equal(Encoding.UTF8.GetBytes(WrappedArabicSrt), await File.ReadAllBytesAsync(copy, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("vtt", ArabicVtt, WrappedArabicVtt)]
    [InlineData("ttml", ArabicTtml, WrappedArabicTtml)]
    public async Task ArabicVttOrTtmlFile_BurnsInFromAFixedCopyOfTheSameFormat(string extension, string subtitle, string expected)
    {
        var source = WriteSubtitle($"Lazyfin Test (2026).ar.{extension}", Encoding.UTF8.GetBytes(subtitle));
        using var state = BurnIn(SubtitleFileNextToTheVideo(source, codec: extension == "vtt" ? "webvtt" : "ttml"));

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}'"));

        var copy = Assert.Single(FixedCopies());
        Assert.Equal(CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(copy)}'"), commandLine);
        Assert.Equal("." + extension, Path.GetExtension(copy));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), await File.ReadAllBytesAsync(copy, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ArabicSrtInWindows1256_BurnsInFromAUtf8CopyWithNoCharsetOption()
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", Windows1256ArabicSrt);
        using var state = BurnIn(SubtitleFileNextToTheVideo(source));
        var options = $":alpha=1:fontsdir='{FontsFolder}',setpts=PTS -0/TB";

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}':charenc=windows-1256{options}"));

        var copy = Assert.Single(FixedCopies());
        Assert.Equal(CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(copy)}'{options}"), commandLine);
        Assert.Equal(Encoding.UTF8.GetBytes("1\r\n00:00:01,000 --> 00:00:03,000\r\n‫مرحبا.‬\r\n\r\n"), await File.ReadAllBytesAsync(copy, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Utf8SrtWithAByteOrderMark_BurnsInFromACopyWithout()
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(ArabicSrt)]);
        using var state = BurnIn(SubtitleFileNextToTheVideo(source));

        await StartFfMpegAsync(ResolveTranscodeManager(), state, CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}':charenc=UTF-8"));

        Assert.Equal(Encoding.UTF8.GetBytes(WrappedArabicSrt), await File.ReadAllBytesAsync(Assert.Single(FixedCopies()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SameSource_ReusesItsCopyAndKeepsItFromTheCacheCleanup()
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", Encoding.UTF8.GetBytes(ArabicSrt));
        var manager = ResolveTranscodeManager();
        var serverCommandLine = CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}'");
        using var first = BurnIn(SubtitleFileNextToTheVideo(source));
        using var second = BurnIn(SubtitleFileNextToTheVideo(source));

        var firstCommandLine = await StartFfMpegAsync(manager, first, serverCommandLine);
        var copy = Assert.Single(FixedCopies());
        await File.WriteAllTextAsync(copy, "already fixed", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(copy, DateTime.UtcNow.AddDays(-29));
        var secondCommandLine = await StartFfMpegAsync(manager, second, serverCommandLine);

        Assert.Equal(firstCommandLine, secondCommandLine);
        Assert.Equal(copy, Assert.Single(FixedCopies()));
        Assert.InRange(File.GetLastWriteTimeUtc(copy), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal("already fixed", await File.ReadAllTextAsync(copy, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChangedSource_GetsANewCopy()
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", Encoding.UTF8.GetBytes(ArabicSrt));
        var manager = ResolveTranscodeManager();
        var serverCommandLine = CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}'");
        using var first = BurnIn(SubtitleFileNextToTheVideo(source));
        using var second = BurnIn(SubtitleFileNextToTheVideo(source));

        var firstCommandLine = await StartFfMpegAsync(manager, first, serverCommandLine);
        await File.WriteAllTextAsync(source, "1\n00:00:01,000 --> 00:00:03,000\nשלום.\n\n", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(source, File.GetLastWriteTimeUtc(source).AddMinutes(1));
        var secondCommandLine = await StartFfMpegAsync(manager, second, serverCommandLine);

        Assert.NotEqual(firstCommandLine, secondCommandLine);
        Assert.Equal(2, FixedCopies().Length);
        Assert.Contains(FixedCopies(), copy => File.ReadAllText(copy) == "1\n00:00:01,000 --> 00:00:03,000\n‫שלום.‬\n\n");
    }

    [Fact]
    public async Task CommandLineWithNoMatchingFilter_StartsUnchanged()
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", Encoding.UTF8.GetBytes(ArabicSrt));
        using var state = BurnIn(SubtitleFileNextToTheVideo(source));
        var serverCommandLine = CommandLineWith($"subtitles='{EscapeSubtitleFilterPath(source)}'");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
        Assert.Empty(FixedCopies());
    }

    [Theory]
    [InlineData("ass")]
    [InlineData("ssa")]
    [InlineData("SSA")]
    public async Task AssOrSsaSource_BurnsInAsAuthored(string codec)
    {
        var source = WriteSubtitle($"Lazyfin Test (2026).ar.{codec.ToLowerInvariant()}", Encoding.UTF8.GetBytes(ArabicAss));
        using var state = BurnIn(SubtitleFileNextToTheVideo(source, codec));
        var serverCommandLine = CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}'");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
        Assert.Empty(FixedCopies());
    }

    [Theory]
    [InlineData("PGSSUB")]
    [InlineData("dvdsub")]
    public async Task ImageSubtitle_BurnsInUnchanged(string codec)
    {
        _server.ExtractedPath = WriteSubtitle($"{MediaSourceId}.srt", Encoding.UTF8.GetBytes(ArabicSrt));
        using var state = BurnIn(new MediaStream { Type = MediaStreamType.Subtitle, Index = SubtitleIndex, Codec = codec, IsExternal = false, Path = VideoPath });
        var serverCommandLine = CommandLineWith("[0:3]scale=1280:720[sub];[0:0][sub]overlay=eof_action=pass:repeatlast=0");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
        Assert.Empty(FixedCopies());
    }

    [Theory]
    [InlineData(SubtitleDeliveryMethod.External)]
    [InlineData(SubtitleDeliveryMethod.Embed)]
    [InlineData(SubtitleDeliveryMethod.Hls)]
    [InlineData(SubtitleDeliveryMethod.Drop)]
    public async Task SubtitleThatIsNotBurnedIn_StartsUnchanged(SubtitleDeliveryMethod method)
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", Encoding.UTF8.GetBytes(ArabicSrt));
        using var state = BurnIn(SubtitleFileNextToTheVideo(source), method);
        var serverCommandLine = CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}'");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
        Assert.Empty(FixedCopies());
    }

    [Fact]
    public async Task TranscodeWithNoSubtitle_StartsUnchanged()
    {
        using var state = new StreamState(_mediaSources.Object, TranscodingJobType.Progressive, Mock.Of<ITranscodeManager>()) { MediaSource = new() };
        var serverCommandLine = CommandLineWith("format=yuv420p");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
    }

    [Fact]
    public async Task EnglishSrt_BurnsInFromTheOriginal()
    {
        var source = WriteSubtitle("Lazyfin Test (2026).srt", Encoding.UTF8.GetBytes("1\r\n00:00:00,500 --> 00:00:04,000\r\nHello, this is the first test line.\r\n\r\n"));
        using var state = BurnIn(SubtitleFileNextToTheVideo(source));
        var serverCommandLine = CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}':charenc=ascii");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
        Assert.Empty(FixedCopies());
    }

    [Theory]
    [InlineData(":charenc=x-no-such-charset")]
    [InlineData("")]
    public async Task SubtitleTheFixCannotDecode_BurnsInUnchanged(string charsetOption)
    {
        var source = WriteSubtitle("Lazyfin Test (2026).ar.srt", Windows1256ArabicSrt);
        using var state = BurnIn(SubtitleFileNextToTheVideo(source));
        var serverCommandLine = CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}'{charsetOption}:alpha=1");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
        Assert.Empty(FixedCopies());
    }

    [Theory]
    [InlineData("Lazyfin Test (2026).ar.sub")]
    [InlineData("Lazyfin Test (2026).ar")]
    public async Task SubtitleFileOfAnotherKind_BurnsInUnchanged(string fileName)
    {
        var source = WriteSubtitle(fileName, Encoding.UTF8.GetBytes(ArabicSrt));
        using var state = BurnIn(SubtitleFileNextToTheVideo(source));
        var serverCommandLine = CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(source)}'");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
        Assert.Empty(FixedCopies());
    }

    [Theory]
    [InlineData("https://subtitles.example/Lazyfin Test (2026).ar.srt")]
    [InlineData("/media/gone/Lazyfin Test (2026).ar.srt")]
    public async Task SubtitleFileTheFixCannotRead_BurnsInUnchanged(string path)
    {
        using var state = BurnIn(SubtitleFileNextToTheVideo(path));
        var serverCommandLine = CommandLineWith($"subtitles=f='{EscapeSubtitleFilterPath(path)}'");

        var commandLine = await StartFfMpegAsync(ResolveTranscodeManager(), state, serverCommandLine);

        Assert.Equal(serverCommandLine, commandLine);
        Assert.Empty(FixedCopies());
    }

    [Fact]
    public async Task OtherTranscodeMembers_AreTheServersOwn()
    {
        var manager = ResolveTranscodeManager();
        using var state = new StreamState(_mediaSources.Object, TranscodingJobType.Dash, Mock.Of<ITranscodeManager>()) { MediaSource = new() };
        using var cancellation = new CancellationTokenSource();

        Assert.Same(_transcodes.Job, manager.GetTranscodingJob("session"));
        Assert.Same(_transcodes.Job, manager.GetTranscodingJob("/cache/transcodes/a.m3u8", TranscodingJobType.Hls));
        manager.PingTranscodingJob("session", true);
        Assert.Same(_transcodes.Kill, manager.KillTranscodingJobs("device", "session", path => path == "x"));
        manager.ReportTranscodingProgress(_transcodes.Job, state, TimeSpan.FromSeconds(5), 24f, 50d, 1024, 4000);
        Assert.Same(_transcodes.Job, manager.OnTranscodeBeginRequest("/cache/transcodes/a.ts", TranscodingJobType.Progressive));
        manager.OnTranscodeEndRequest(_transcodes.Job);
        Assert.Same(_transcodes.Lock, await manager.LockAsync("/cache/transcodes/a.m3u8", cancellation.Token));

        Assert.Equal(
            [
                "job session",
                "job /cache/transcodes/a.m3u8 Hls",
                "ping session True",
                "kill device session True",
                "progress True Dash 00:00:05 24 50 1024 4000",
                "begin /cache/transcodes/a.ts Progressive",
                "end True",
                "lock /cache/transcodes/a.m3u8",
            ],
            _transcodes.Requests);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _temp.Delete(recursive: true);
        _transcodes.Dispose();
    }

    /// <summary>
    /// Writes a subtitle file into the temporary media folder, whose name has an apostrophe that ffmpeg needs escaped.
    /// </summary>
    /// <returns>The file's path.</returns>
    private string WriteSubtitle(string fileName, byte[] bytes)
    {
        var path = Path.Combine(MediaFolder, fileName);
        Directory.CreateDirectory(MediaFolder);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// The files the RTL fix wrote into the server's cache.
    /// </summary>
    private string[] FixedCopies()
        => Directory.Exists(CachePath) ? Directory.GetFiles(CachePath, "*", SearchOption.AllDirectories) : [];

    private ITranscodeManager ResolveTranscodeManager(Action<IServiceCollection>? registerServer = null)
    {
        var provider = BuildServices(registerServer ?? (services => services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>()));
        return provider.GetRequiredService<ITranscodeManager>();
    }

    /// <summary>
    /// Builds the stream state of a transcode the way Jellyfin's <c>StreamingHelpers</c> does for the subtitle the viewer picked.
    /// </summary>
    private StreamState BurnIn(MediaStream subtitle, SubtitleDeliveryMethod method = SubtitleDeliveryMethod.Encode)
        => new(_mediaSources.Object, TranscodingJobType.Hls, Mock.Of<ITranscodeManager>())
        {
            MediaSource = new MediaSourceInfo { Id = MediaSourceId, Path = VideoPath },
            SubtitleStream = subtitle,
            SubtitleDeliveryMethod = method,
        };

    /// <summary>
    /// Starts ffmpeg the way Jellyfin's <c>DynamicHlsController</c> does.
    /// </summary>
    /// <returns>The command line the server's transcode manager received.</returns>
    private async Task<string> StartFfMpegAsync(ITranscodeManager manager, StreamState state, string commandLine)
    {
        using var cancellation = new CancellationTokenSource();
        await manager.StartFfMpeg(state, OutputPath, commandLine, UserId, TranscodingJobType.Hls, cancellation);
        return _transcodes.CommandLines[^1];
    }

    private static MediaStream SubtitleFileNextToTheVideo(string path, string codec = "subrip")
        => new() { Type = MediaStreamType.Subtitle, Index = SubtitleIndex, Codec = codec, Language = "ara", IsExternal = true, Path = path };

    /// <summary>
    /// A command line as Jellyfin 13.0 writes it for an HLS transcode, with <paramref name="filter"/> last in the video filters.
    /// </summary>
    private static string CommandLineWith(string filter)
        => "-analyzeduration 200M -probesize 1G  -i file:\"/media/Movies/Lazyfin Test (2026)/Lazyfin Test (2026).mkv\" "
            + "-map_metadata -1 -map_chapters -1 -threads 0 -map 0:0 -map 0:1 -codec:v:0 libx264 -preset veryfast -crf 23 "
            + "-force_key_frames:0 \"expr:gte(t,n_forced*3)\" -sc_threshold:v:0 0 "
            + "-vf \"setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709,scale=trunc(min(max(iw\\,ih*a)\\,416)/2)*2:trunc(ow/a/2)*2,format=yuv420p,"
            + filter
            + "\" -start_at_zero -codec:a:0 copy -copyts -avoid_negative_ts disabled -max_muxing_queue_size 2048 -f hls "
            + $"-hls_time 3 -hls_segment_type mpegts -start_number 0 -hls_playlist_type vod -hls_list_size 0 -y \"{OutputPath}\"";

    /// <summary>
    /// Escapes a path for an ffmpeg filter the way Jellyfin's <c>MediaEncoder.EscapeSubtitleFilterPath</c> does.
    /// </summary>
    private static string EscapeSubtitleFilterPath(string path) => path
        .Replace('\\', '/')
        .Replace(":", "\\:", StringComparison.Ordinal)
        .Replace("'", @"'\\\''", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    /// <summary>
    /// What the fake server transcode manager was asked, and how often it was built and disposed.
    /// </summary>
    private sealed class ServerTranscodes : IDisposable
    {
        public TranscodingJob Job { get; } = new(NullLogger<TranscodingJob>.Instance);

        public IDisposable Lock { get; } = new MemoryStream();

        public Task Kill { get; } = Task.Delay(1);

        public List<string> Requests { get; } = [];

        public List<string> CommandLines { get; } = [];

        public int ManagersBuilt { get; set; }

        public int ManagersDisposed { get; set; }

        public void Dispose()
        {
            Job.Dispose();
            Lock.Dispose();
        }
    }

    /// <summary>
    /// Stands in for the server's own <c>TranscodeManager</c>.
    /// </summary>
    private sealed class FakeTranscodeManager : ITranscodeManager, IDisposable
    {
        private readonly ServerTranscodes _transcodes;

        public FakeTranscodeManager(ServerTranscodes transcodes)
        {
            _transcodes = transcodes;
            _transcodes.ManagersBuilt++;
        }

        public TranscodingJob? GetTranscodingJob(string playSessionId)
        {
            _transcodes.Requests.Add($"job {playSessionId}");
            return _transcodes.Job;
        }

        public TranscodingJob? GetTranscodingJob(string path, TranscodingJobType type)
        {
            _transcodes.Requests.Add($"job {path} {type}");
            return _transcodes.Job;
        }

        public void PingTranscodingJob(string playSessionId, bool? isUserPaused)
            => _transcodes.Requests.Add($"ping {playSessionId} {isUserPaused}");

        public Task KillTranscodingJobs(string deviceId, string? playSessionId, Func<string, bool> deleteFiles)
        {
            _transcodes.Requests.Add($"kill {deviceId} {playSessionId} {deleteFiles("x")}");
            return _transcodes.Kill;
        }

        public void ReportTranscodingProgress(TranscodingJob job, StreamState state, TimeSpan? transcodingPosition, float? framerate, double? percentComplete, long? bytesTranscoded, int? bitRate)
            => _transcodes.Requests.Add($"progress {ReferenceEquals(job, _transcodes.Job)} {state.TranscodingType} {transcodingPosition} {framerate} {percentComplete} {bytesTranscoded} {bitRate}");

        public Task<TranscodingJob> StartFfMpeg(StreamState state, string outputPath, string commandLineArguments, Guid userId, TranscodingJobType transcodingJobType, CancellationTokenSource cancellationTokenSource, string? workingDirectory = null)
        {
            _transcodes.Requests.Add($"start {outputPath} {userId:N} {transcodingJobType} {workingDirectory}");
            _transcodes.CommandLines.Add(commandLineArguments);
            return Task.FromResult(_transcodes.Job);
        }

        public TranscodingJob? OnTranscodeBeginRequest(string path, TranscodingJobType type)
        {
            _transcodes.Requests.Add($"begin {path} {type}");
            return _transcodes.Job;
        }

        public void OnTranscodeEndRequest(TranscodingJob job)
            => _transcodes.Requests.Add($"end {ReferenceEquals(job, _transcodes.Job)}");

        public ValueTask<IDisposable> LockAsync(string outputPath, CancellationToken cancellationToken)
        {
            _transcodes.Requests.Add($"lock {outputPath}");
            return ValueTask.FromResult(_transcodes.Lock);
        }

        public void Dispose() => _transcodes.ManagersDisposed++;
    }

    /// <summary>
    /// Stands in for another plugin that decorated the server's transcode manager before Lazyfin did.
    /// </summary>
    private sealed class OtherPluginTranscodeManager(ITranscodeManager inner, ServerTranscodes transcodes) : ITranscodeManager
    {
        public TranscodingJob? GetTranscodingJob(string playSessionId) => inner.GetTranscodingJob(playSessionId);

        public TranscodingJob? GetTranscodingJob(string path, TranscodingJobType type) => inner.GetTranscodingJob(path, type);

        public void PingTranscodingJob(string playSessionId, bool? isUserPaused) => inner.PingTranscodingJob(playSessionId, isUserPaused);

        public Task KillTranscodingJobs(string deviceId, string? playSessionId, Func<string, bool> deleteFiles)
            => inner.KillTranscodingJobs(deviceId, playSessionId, deleteFiles);

        public void ReportTranscodingProgress(TranscodingJob job, StreamState state, TimeSpan? transcodingPosition, float? framerate, double? percentComplete, long? bytesTranscoded, int? bitRate)
            => inner.ReportTranscodingProgress(job, state, transcodingPosition, framerate, percentComplete, bytesTranscoded, bitRate);

        public Task<TranscodingJob> StartFfMpeg(StreamState state, string outputPath, string commandLineArguments, Guid userId, TranscodingJobType transcodingJobType, CancellationTokenSource cancellationTokenSource, string? workingDirectory = null)
        {
            transcodes.Requests.Add("other plugin");
            return inner.StartFfMpeg(state, outputPath, commandLineArguments, userId, transcodingJobType, cancellationTokenSource, workingDirectory);
        }

        public TranscodingJob? OnTranscodeBeginRequest(string path, TranscodingJobType type) => inner.OnTranscodeBeginRequest(path, type);

        public void OnTranscodeEndRequest(TranscodingJob job) => inner.OnTranscodeEndRequest(job);

        public ValueTask<IDisposable> LockAsync(string outputPath, CancellationToken cancellationToken) => inner.LockAsync(outputPath, cancellationToken);
    }
}
