using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.Lazyfin.Tests.Enhancements.RtlFix;

/// <summary>
/// Checks what Jellyfin sees after <see cref="RtlFixServiceCollectionExtensions.AddRtlFix"/>: the services it resolves, and
/// the subtitles they hand to a client.
/// </summary>
/// <remarks>
/// Each test registers a fake server <see cref="ISubtitleEncoder"/> the way Jellyfin's <c>ApplicationHost</c> does,
/// calls <c>AddRtlFix</c>, resolves the interface, and calls it the way Jellyfin's <c>SubtitleController</c> does.
/// RLE is U+202B and PDF is U+202C.
/// </remarks>
public sealed partial class AddRtlFixTests
{
    private const string MediaSourceId = "6f1c2a52b0e94b6f9d5c0a4e7b1d3c28";

    private const int SubtitleIndex = 2;

    private const string ArabicSrt = "1\n00:00:01,000 --> 00:00:03,000\n<i>مرحبا.</i>\nكيف حالك؟\n\n";

    private const string WrappedArabicSrt = "1\n00:00:01,000 --> 00:00:03,000\n<i>\u202Bمرحبا.\u202C</i>\n\u202Bكيف حالك؟\u202C\n\n";

    private const string ArabicJson =
        """{"TrackEvents":[{"Id":"1","Text":"\u003Ci\u003E\u0645\u0631\u062D\u0628\u0627.\u003C/i\u003E\n\u0643\u064A\u0641 \u062D\u0627\u0644\u0643\u061F","StartPositionTicks":10000000,"EndPositionTicks":30000000}]}""";

    private const string WrappedArabicJson =
        """{"TrackEvents":[{"Id":"1","Text":"\u003Ci\u003E\u202B\u0645\u0631\u062D\u0628\u0627.\u202C\u003C/i\u003E\n\u202B\u0643\u064A\u0641 \u062D\u0627\u0644\u0643\u061F\u202C","StartPositionTicks":10000000,"EndPositionTicks":30000000}]}""";

    private const string ArabicVtt = "WEBVTT\n\n00:00:01.000 --> 00:00:03.000\n<i>مرحبا.</i>\nكيف حالك؟";

    private const string WrappedArabicVtt = "WEBVTT\n\n00:00:01.000 --> 00:00:03.000\n<i>\u202Bمرحبا.\u202C</i>\n\u202Bكيف حالك؟\u202C";

    private const string ArabicTtml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<tt xmlns=\"http://www.w3.org/ns/ttml\">\n  <body>\n    <div>\n"
        + "      <p begin=\"1.0s\" xml:id=\"p0\" end=\"3.0s\">مرحبا.<br />كيف حالك؟</p>\n    </div>\n  </body>\n</tt>";

    private const string WrappedArabicTtml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<tt xmlns=\"http://www.w3.org/ns/ttml\">\n  <body>\n    <div>\n"
        + "      <p begin=\"1.0s\" xml:id=\"p0\" end=\"3.0s\">\u202Bمرحبا.\u202C<br />\u202Bكيف حالك؟\u202C</p>\n    </div>\n  </body>\n</tt>";

    private const string ArabicAss =
        "[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n"
        + "Dialogue: 0,0:00:01.00,0:00:03.00,Default,,0,0,0,,{\\i1}مرحبا.{\\i0}\\Nكيف حالك؟\n";

    private readonly Video _item = new() { Id = Guid.Parse(MediaSourceId) };

    private readonly ServerSubtitles _server = new();

    private readonly Mock<IMediaSourceManager> _mediaSources = new();

    [Theory]
    [InlineData("srt")]
    [InlineData("subrip")]
    [InlineData("SRT")]
    public async Task ArabicSrtFile_IsWrappedForEverySrtFormatName(string format)
    {
        HasSubtitleStream("subrip");
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var srt = await GetSubtitlesAsync(ResolveSubtitleEncoder(), format);

        Assert.Equal(WrappedArabicSrt, Encoding.UTF8.GetString(srt));
    }

    [Fact]
    public async Task ArabicSrtFile_IsWrappedAsJsonForTheWebPlayer()
    {
        HasSubtitleStream("subrip");
        _server.Output = Encoding.UTF8.GetBytes(ArabicJson);

        var json = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "json");

        Assert.Equal(WrappedArabicJson, Encoding.UTF8.GetString(json));
    }

    [Theory]
    [InlineData("vtt", ArabicVtt, WrappedArabicVtt)]
    [InlineData("webvtt", ArabicVtt, WrappedArabicVtt)]
    [InlineData("VTT", ArabicVtt, WrappedArabicVtt)]
    [InlineData("ttml", ArabicTtml, WrappedArabicTtml)]
    public async Task ArabicSrtFile_IsWrappedAsWebVttForHlsAndAsTtml(string format, string output, string expected)
    {
        HasSubtitleStream("subrip");
        _server.Output = Encoding.UTF8.GetBytes(output);

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), format);

        Assert.Equal(expected, Encoding.UTF8.GetString(subtitle));
    }

    [Fact]
    public async Task ArabicVttFile_IsWrappedWhenPassedThrough()
    {
        HasSubtitleStream("webvtt");
        _server.Output = Encoding.UTF8.GetBytes(ArabicVtt);

        var vtt = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "vtt");

        Assert.Equal(WrappedArabicVtt, Encoding.UTF8.GetString(vtt));
    }

    [Theory]
    [InlineData("ass")]
    [InlineData("ssa")]
    public async Task AssOrSsaOutput_ComesBackUnchanged(string format)
    {
        HasSubtitleStream("subrip");
        _server.Output = Encoding.UTF8.GetBytes(ArabicAss);

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), format);

        Assert.Equal(_server.Output, subtitle);
    }

    [Theory]
    [InlineData("srt", ArabicSrt, WrappedArabicSrt)]
    [InlineData("json", ArabicJson, WrappedArabicJson)]
    [InlineData("vtt", ArabicVtt, WrappedArabicVtt)]
    [InlineData("ttml", ArabicTtml, WrappedArabicTtml)]
    public async Task ByteOrderMark_IsKept(string format, string output, string expected)
    {
        HasSubtitleStream("subrip");
        _server.Output = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(output)];

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), format);

        Assert.Equal([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(expected)], subtitle);
    }

    [Fact]
    public async Task ArabicSrtInWindows1256_ComesBackAsTheServerSentIt()
    {
        HasSubtitleStream("subrip");
        byte[] windows1256Line = [0xE3, 0xD1, 0xCD, 0xC8, 0xC7, (byte)'.'];
        _server.Output = [.. Encoding.ASCII.GetBytes("1\r\n00:00:01,000 --> 00:00:03,000\r\n"), .. windows1256Line, .. "\r\n\r\n"u8];

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "srt");

        Assert.Equal(_server.Output, subtitle);
    }

    [Fact]
    public async Task EnglishSrt_ComesBackByteForByte()
    {
        HasSubtitleStream("subrip");
        _server.Output = Encoding.UTF8.GetBytes("1\r\n00:00:00,500 --> 00:00:04,000\r\nHello, this is the first test line.\r\n\r\n");

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "srt");

        Assert.Equal(_server.Output, subtitle);
    }

    [Fact]
    public async Task ArabicSrtTrackInsideTheVideo_IsWrapped()
    {
        HasSubtitleStream("subrip", isExternal: false);
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var srt = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "srt");

        Assert.Equal(WrappedArabicSrt, Encoding.UTF8.GetString(srt));
    }

    [Theory]
    [InlineData("ass", "srt", ArabicSrt)]
    [InlineData("ass", "json", ArabicJson)]
    [InlineData("ass", "vtt", ArabicVtt)]
    [InlineData("ass", "ttml", ArabicTtml)]
    [InlineData("ssa", "srt", ArabicSrt)]
    [InlineData("SSA", "json", ArabicJson)]
    [InlineData("ssa", "webvtt", ArabicVtt)]
    public async Task AssOrSsaSource_ComesBackAsAuthored(string codec, string format, string output)
    {
        HasSubtitleStream(codec);
        _server.Output = Encoding.UTF8.GetBytes(output);

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), format);

        Assert.Equal(_server.Output, subtitle);
    }

    [Theory]
    [InlineData("PGSSUB")]
    [InlineData("dvdsub")]
    public async Task ImageSubtitle_ComesBackUnchanged(string codec)
    {
        HasSubtitleStream(codec, isExternal: false);
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "srt");

        Assert.Equal(_server.Output, subtitle);
    }

    [Fact]
    public async Task StreamTheServerCannotFind_ComesBackUnchanged()
    {
        _mediaSources
            .Setup(m => m.GetStaticMediaSources(_item, It.IsAny<bool>(), It.IsAny<User>()))
            .Returns([new MediaSourceInfo { Id = MediaSourceId, MediaStreams = [] }]);
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "srt");

        Assert.Equal(_server.Output, subtitle);
    }

    [Fact]
    public async Task MediaSourceTheServerCannotFind_ComesBackUnchanged()
    {
        _mediaSources
            .Setup(m => m.GetStaticMediaSources(_item, It.IsAny<bool>(), It.IsAny<User>()))
            .Returns([]);
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "srt");

        Assert.Equal(_server.Output, subtitle);
    }

    [Fact]
    public async Task MediaSourceLookupThatFails_ComesBackUnchanged()
    {
        _mediaSources
            .Setup(m => m.GetStaticMediaSources(_item, It.IsAny<bool>(), It.IsAny<User>()))
            .Throws<InvalidCastException>();
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var subtitle = await GetSubtitlesAsync(ResolveSubtitleEncoder(), "srt");

        Assert.Equal(_server.Output, subtitle);
    }

    [Fact]
    public void SubtitleEncoder_KeepsOneServerEncoderWhenResolvedManyTimes()
    {
        using var provider = BuildServices(services => services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>());

        var first = provider.GetRequiredService<ISubtitleEncoder>();
        var second = provider.GetRequiredService<ISubtitleEncoder>();

        Assert.Same(first, second);
        Assert.Equal(1, _server.EncodersBuilt);
    }

    [Fact]
    public async Task DecoratorAnotherPluginRegisteredFirst_StaysInTheChain()
    {
        HasSubtitleStream("subrip");
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var encoder = ResolveSubtitleEncoder(services =>
        {
            services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>();
            services.RemoveAll<ISubtitleEncoder>();
            services.AddSingleton<FakeSubtitleEncoder>();
            services.AddSingleton<ISubtitleEncoder>(sp => new OtherPluginSubtitleEncoder(sp.GetRequiredService<FakeSubtitleEncoder>(), _server));
        });
        var srt = await GetSubtitlesAsync(encoder, "srt");

        Assert.Equal(WrappedArabicSrt, Encoding.UTF8.GetString(srt));
        Assert.Equal(["other plugin", $"{MediaSourceId} {MediaSourceId} {SubtitleIndex} srt"], _server.Requests);
    }

    [Fact]
    public async Task EncoderRegisteredAsAnInstance_IsWrapped()
    {
        HasSubtitleStream("subrip");
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var encoder = ResolveSubtitleEncoder(services => services.AddSingleton<ISubtitleEncoder>(new FakeSubtitleEncoder(_server)));
        var srt = await GetSubtitlesAsync(encoder, "srt");

        Assert.Equal(WrappedArabicSrt, Encoding.UTF8.GetString(srt));
    }

    [Fact]
    public void KeyedEncoderRegisteredLater_IsLeftAlone()
    {
        using var provider = BuildServices(services =>
        {
            services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>();
            services.AddKeyedSingleton<ISubtitleEncoder, FakeSubtitleEncoder>("other plugin");
        });

        Assert.IsType<FakeSubtitleEncoder>(provider.GetRequiredKeyedService<ISubtitleEncoder>("other plugin"));
        Assert.IsNotType<FakeSubtitleEncoder>(provider.GetRequiredService<ISubtitleEncoder>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ServicesMissingAServerEncoderOrTranscodeManager_AreLeftUnchanged(bool hasEncoder, bool hasTranscodeManager)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_server);
        services.AddKeyedSingleton<ISubtitleEncoder, FakeSubtitleEncoder>("other plugin");
        services.AddKeyedSingleton<ITranscodeManager, FakeTranscodeManager>("other plugin");
        if (hasEncoder)
        {
            services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>();
        }

        if (hasTranscodeManager)
        {
            services.AddSingleton<ITranscodeManager, FakeTranscodeManager>();
        }

        var before = services.ToList();

        services.AddRtlFix();

        Assert.Equal(before, services);
    }

    [Fact]
    public async Task LazyfinServiceRegistrator_TurnsTheRtlFixOn()
    {
        HasSubtitleStream("subrip");
        _server.Output = Encoding.UTF8.GetBytes(ArabicSrt);

        var encoder = ResolveSubtitleEncoder(
            services => services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>(),
            services => new PluginServiceRegistrator().RegisterServices(services, Mock.Of<IServerApplicationHost>()));
        var srt = await GetSubtitlesAsync(encoder, "srt");

        Assert.Equal(WrappedArabicSrt, Encoding.UTF8.GetString(srt));
    }

    [Fact]
    public async Task SubtitleFileCharacterSet_IsTheServersOwn()
    {
        var (stream, source) = (new MediaStream { Index = SubtitleIndex }, new MediaSourceInfo { Id = MediaSourceId });

        var charset = await ResolveSubtitleEncoder().GetSubtitleFileCharacterSet(stream, "ara", source, CancellationToken.None);

        Assert.Equal(ServerSubtitles.CharacterSet, charset);
        Assert.Equal([$"charset {SubtitleIndex} ara {MediaSourceId}"], _server.Requests);
    }

    [Fact]
    public async Task SubtitleFilePath_IsTheServersOwn()
    {
        var (stream, source) = (new MediaStream { Index = SubtitleIndex }, new MediaSourceInfo { Id = MediaSourceId });

        var path = await ResolveSubtitleEncoder().GetSubtitleFilePath(stream, source, CancellationToken.None);

        Assert.Equal(_server.ExtractedPath, path);
        Assert.Equal([$"path {SubtitleIndex} {MediaSourceId}"], _server.Requests);
    }

    [Fact]
    public void ExtractAllExtractableSubtitles_IsTheServersOwn()
    {
        var source = new MediaSourceInfo { Id = MediaSourceId };

        var extraction = ResolveSubtitleEncoder().ExtractAllExtractableSubtitles(source, CancellationToken.None);

        Assert.Same(_server.Extraction, extraction);
        Assert.Equal([$"extract {MediaSourceId}"], _server.Requests);
    }

    /// <summary>
    /// Builds the services the way Jellyfin's <c>ApplicationHost</c> does, lets a plugin in, and resolves the subtitle encoder.
    /// The server's transcode manager is registered first, so <paramref name="registerServer"/> can replace it.
    /// </summary>
    /// <param name="registerServer">Registers the server's encoder. By default, the way <c>ApplicationHost</c> does.</param>
    /// <param name="registerPlugin">Lets the plugin in. By default, <c>AddRtlFix</c>.</param>
    private ISubtitleEncoder ResolveSubtitleEncoder(Action<IServiceCollection>? registerServer = null, Action<IServiceCollection>? registerPlugin = null)
    {
        var provider = BuildServices(registerServer ?? (services => services.AddSingleton<ISubtitleEncoder, FakeSubtitleEncoder>()), registerPlugin);
        return provider.GetRequiredService<ISubtitleEncoder>();
    }

    private ServiceProvider BuildServices(Action<IServiceCollection> registerServer, Action<IServiceCollection>? registerPlugin = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(_server);
        services.AddSingleton(_mediaSources.Object);
        services.AddSingleton(_mediaEncoder.Object);
        services.AddSingleton(_paths.Object);
        services.AddSingleton(_transcodes);
        services.AddSingleton<ITranscodeManager, FakeTranscodeManager>();
        registerServer(services);

        (registerPlugin ?? (s => s.AddRtlFix()))(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Gives the item one media source with one subtitle stream at <see cref="SubtitleIndex"/>.
    /// </summary>
    private void HasSubtitleStream(string codec, bool isExternal = true)
    {
        var stream = new MediaStream
        {
            Type = MediaStreamType.Subtitle,
            Index = SubtitleIndex,
            Codec = codec,
            IsExternal = isExternal,
            Path = isExternal ? "/media/Movies/Lazyfin Test (2026)/Lazyfin Test (2026).ar.srt" : null,
        };
        var source = new MediaSourceInfo { Id = MediaSourceId, MediaStreams = [stream] };
        _mediaSources
            .Setup(m => m.GetStaticMediaSources(_item, It.IsAny<bool>(), It.IsAny<User>()))
            .Returns([source]);
    }

    /// <summary>
    /// Asks for a subtitle the way <c>SubtitleController.GetSubtitle</c> does, after it maps <c>js</c> to <c>json</c>.
    /// </summary>
    private async Task<byte[]> GetSubtitlesAsync(ISubtitleEncoder encoder, string format)
    {
        var stream = await encoder.GetSubtitles(_item, MediaSourceId, SubtitleIndex, format, 0, 0, false, CancellationToken.None);
        await using (stream)
        {
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes);
            return bytes.ToArray();
        }
    }

    /// <summary>
    /// What the fake server encoder hands out, and what it was asked.
    /// </summary>
    private sealed class ServerSubtitles
    {
        public const string CharacterSet = "windows-1256";

        public string ExtractedPath { get; set; } = "/cache/subtitles/6f1c2a52b0e94b6f9d5c0a4e7b1d3c28.srt";

        public byte[] Output { get; set; } = [];

        public Task Extraction { get; } = Task.Delay(1);

        public List<string> Requests { get; } = [];

        public int EncodersBuilt { get; set; }
    }

    /// <summary>
    /// Stands in for the server's own <c>SubtitleEncoder</c>.
    /// </summary>
    private sealed class FakeSubtitleEncoder : ISubtitleEncoder
    {
        private readonly ServerSubtitles _subtitles;

        public FakeSubtitleEncoder(ServerSubtitles subtitles)
        {
            _subtitles = subtitles;
            _subtitles.EncodersBuilt++;
        }

        public Task<Stream> GetSubtitles(BaseItem item, string mediaSourceId, int subtitleStreamIndex, string outputFormat, long startTimeTicks, long endTimeTicks, bool preserveOriginalTimestamps, CancellationToken cancellationToken)
        {
            _subtitles.Requests.Add($"{item.Id:N} {mediaSourceId} {subtitleStreamIndex} {outputFormat}");
            return Task.FromResult<Stream>(new MemoryStream(_subtitles.Output));
        }

        public Task<string> GetSubtitleFileCharacterSet(MediaStream subtitleStream, string language, MediaSourceInfo mediaSource, CancellationToken cancellationToken)
        {
            _subtitles.Requests.Add($"charset {subtitleStream.Index} {language} {mediaSource.Id}");
            return Task.FromResult(ServerSubtitles.CharacterSet);
        }

        public Task<string> GetSubtitleFilePath(MediaStream subtitleStream, MediaSourceInfo mediaSource, CancellationToken cancellationToken)
        {
            _subtitles.Requests.Add($"path {subtitleStream.Index} {mediaSource.Id}");
            return Task.FromResult(_subtitles.ExtractedPath);
        }

        public Task ExtractAllExtractableSubtitles(MediaSourceInfo mediaSource, CancellationToken cancellationToken)
        {
            _subtitles.Requests.Add($"extract {mediaSource.Id}");
            return _subtitles.Extraction;
        }
    }

    /// <summary>
    /// Stands in for another plugin that decorated the server's encoder before Lazyfin did.
    /// </summary>
    private sealed class OtherPluginSubtitleEncoder(ISubtitleEncoder inner, ServerSubtitles subtitles) : ISubtitleEncoder
    {
        public Task<Stream> GetSubtitles(BaseItem item, string mediaSourceId, int subtitleStreamIndex, string outputFormat, long startTimeTicks, long endTimeTicks, bool preserveOriginalTimestamps, CancellationToken cancellationToken)
        {
            subtitles.Requests.Add("other plugin");
            return inner.GetSubtitles(item, mediaSourceId, subtitleStreamIndex, outputFormat, startTimeTicks, endTimeTicks, preserveOriginalTimestamps, cancellationToken);
        }

        public Task<string> GetSubtitleFileCharacterSet(MediaStream subtitleStream, string language, MediaSourceInfo mediaSource, CancellationToken cancellationToken)
            => inner.GetSubtitleFileCharacterSet(subtitleStream, language, mediaSource, cancellationToken);

        public Task<string> GetSubtitleFilePath(MediaStream subtitleStream, MediaSourceInfo mediaSource, CancellationToken cancellationToken)
            => inner.GetSubtitleFilePath(subtitleStream, mediaSource, cancellationToken);

        public Task ExtractAllExtractableSubtitles(MediaSourceInfo mediaSource, CancellationToken cancellationToken)
            => inner.ExtractAllExtractableSubtitles(mediaSource, cancellationToken);
    }
}
