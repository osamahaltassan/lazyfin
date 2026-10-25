using System;
using Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;
using Xunit;

namespace Jellyfin.Plugin.Lazyfin.Tests.Enhancements.RtlFix;

/// <summary>
/// Checks what a viewer sees after <see cref="RtlLineRule.Apply"/>: every RTL line wrapped in RLE and PDF, every other byte as it was.
/// </summary>
/// <remarks>
/// The samples are the ones in the answer to LAZ-4, as a Linux server writes them. The JSON bodies were written by a
/// copy of Jellyfin's <c>JsonWriter</c>, so their <c>\uXXXX</c> escapes are the server's own.
/// RLE is U+202B and PDF is U+202C.
/// </remarks>
public class RtlLineRuleTests
{
    private const string SrtSample =
        "1\n00:00:01,000 --> 00:00:03,000\n<i>مرحبا.</i>\nكيف حالك؟\n\n2\n00:00:04,000 --> 00:00:05,500\n{\\an8}OK.\n\n";

    private const string JsonSample =
        """{"TrackEvents":[{"Id":"1","Text":"\u003Ci\u003E\u0645\u0631\u062D\u0628\u0627.\u003C/i\u003E\n\u0643\u064A\u0641 \u062D\u0627\u0644\u0643\u061F","StartPositionTicks":10000000,"EndPositionTicks":30000000},{"Id":"2","Text":"{\\an8}OK.","StartPositionTicks":40000000,"EndPositionTicks":55000000}]}""";

    private const string JsonHebrewSample =
        """{"TrackEvents":[{"Id":"1","Text":"\u05E9\u05DC\u05D5\u05DD, \u05DE\u05D4 \u05E9\u05DC\u05D5\u05DE\u05DA?\r\nI\u0027m fine \u0026 you?","StartPositionTicks":10000000,"EndPositionTicks":30000000}]}""";

    private const string JsonWithUnknownPropertiesSample =
        """{"TrackEvents":[{"Id":"1","Text":"\u0645\u0631\u062D\u0628\u0627.","StartPositionTicks":10000000,"EndPositionTicks":30000000,"Style":{"Name":"Default","Italic":true}}],"Version":2,"Notes":null}""";

    private const string JsonEnglishIndentedSample =
        "{\n  \"TrackEvents\": [\n    { \"Id\": \"2\", \"Text\": \"{\\\\an8}OK.\", \"StartPositionTicks\": 40000000, \"EndPositionTicks\": 55000000 }\n  ]\n}\n";

    private const string AssSample =
        "[Script Info]\r\n; This is an Advanced Sub Station Alpha v4+ script.\r\nTitle: untitled\r\nScriptType: v4.00+\r\nPlayDepth: 0\r\nScaledBorderAndShadow: Yes\r\n\r\n"
        + "[V4+ Styles]\r\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\r\n"
        + "Style: Default,Arial,20,&H00FFFFFF,&H0300FFFF,&H00000000,&H02000000,0,0,0,0,100,100,0,0,1,2,1,2,10,10,10,1\r\n\r\n"
        + "[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n"
        + "Dialogue: 0,0:00:01.00,0:00:03.00,Default,,0,0,0,,{\\i1}مرحبا.{\\i0}\\Nكيف حالك؟\n"
        + "Dialogue: 0,0:00:04.00,0:00:05.50,Default,,0,0,0,,{\\an8}OK.\n";

    private const string SsaSample =
        "[Script Info]\r\n; This is a Sub Station Alpha v4 script.\r\nTitle: untitled\r\nScriptType: v4.00\r\nCollisions: Normal\r\nPlayDepth: 0\r\n\r\n"
        + "[V4 Styles]\r\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding\r\n"
        + "Style: Default,Arial,20,16777215,65535,65535,-2147483640,0,0,1,2,1,2,10,10,10,0,1\r\n\r\n"
        + "[Events]\r\nFormat: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n"
        + "Dialogue: Marked=0,0:00:01.00,0:00:03.00,*Default,NTP,0000,0000,0000,,{\\i1}مرحبا.{\\i0}\\Nكيف حالك؟\n"
        + "Dialogue: Marked=0,0:00:04.00,0:00:05.50,*Default,NTP,0000,0000,0000,,{\\an8}OK.\n";

    /// <summary>
    /// Gets every sample in this class, with its format.
    /// </summary>
    public static TheoryData<string, SubtitleTextFormat> EverySample => new()
    {
        { SrtSample, SubtitleTextFormat.SubRip },
        { "\uFEFF" + SrtSample.Replace("\n", "\r\n", StringComparison.Ordinal), SubtitleTextFormat.SubRip },
        { SrtSample.Replace('\n', '\r'), SubtitleTextFormat.SubRip },
        { SrtCue("שלום, מה שלומך?"), SubtitleTextFormat.SubRip },
        { SrtCue("شاهدت فيلم Inception أمس!"), SubtitleTextFormat.SubRip },
        { SrtCue("I said مرحبا to her."), SubtitleTextFormat.SubRip },
        { SrtCue("...", "♪"), SubtitleTextFormat.SubRip },
        { SrtCue("{\\an8}<font color=\"red\"><i>مرحبا.</i></font>"), SubtitleTextFormat.SubRip },
        { SrtCue("<i>مرحبا</i> يا <b>صديقي</b>."), SubtitleTextFormat.SubRip },
        { SrtCue("&lrm;مرحبا.", "&rlm;OK.", "&amp; مرحبا."), SubtitleTextFormat.SubRip },
        { JsonSample, SubtitleTextFormat.Json },
        { JsonHebrewSample, SubtitleTextFormat.Json },
        { JsonWithUnknownPropertiesSample, SubtitleTextFormat.Json },
        { JsonEnglishIndentedSample, SubtitleTextFormat.Json },
        { "\uFEFF" + JsonSample, SubtitleTextFormat.Json },
        { AssSample, SubtitleTextFormat.Ass },
        { SsaSample, SubtitleTextFormat.Ssa },
    };

    [Fact]
    public void SrtArabicLineEndingInPeriod_IsWrapped()
    {
        var fixedSrt = RtlLineRule.Apply(SrtCue("مرحبا."), SubtitleTextFormat.SubRip);

        Assert.Equal("1\n00:00:01,000 --> 00:00:03,000\n\u202Bمرحبا.\u202C\n\n", fixedSrt);
    }

    [Fact]
    public void SrtHebrewLine_IsWrapped()
    {
        var fixedSrt = RtlLineRule.Apply(SrtCue("שלום, מה שלומך?"), SubtitleTextFormat.SubRip);

        Assert.Equal(SrtCue("\u202Bשלום, מה שלומך?\u202C"), fixedSrt);
    }

    [Fact]
    public void SrtArabicLineQuotingAnEnglishWord_IsWrapped()
    {
        var fixedSrt = RtlLineRule.Apply(SrtCue("شاهدت فيلم Inception أمس!"), SubtitleTextFormat.SubRip);

        Assert.Equal(SrtCue("\u202Bشاهدت فيلم Inception أمس!\u202C"), fixedSrt);
    }

    [Theory]
    [InlineData("I said مرحبا to her.")]
    [InlineData("...")]
    [InlineData("♪")]
    [InlineData("- 2024!")]
    public void SrtLineThatDoesNotStartRightToLeft_StaysTheSame(string line)
    {
        var srt = SrtCue(line);

        Assert.Equal(srt, RtlLineRule.Apply(srt, SubtitleTextFormat.SubRip));
    }

    [Theory]
    [InlineData("{\\an8}مرحبا.", "{\\an8}\u202Bمرحبا.\u202C")]
    [InlineData("<i>مرحبا.</i>", "<i>\u202Bمرحبا.\u202C</i>")]
    [InlineData("<b>مرحبا.</b>", "<b>\u202Bمرحبا.\u202C</b>")]
    [InlineData("<u>مرحبا.</u>", "<u>\u202Bمرحبا.\u202C</u>")]
    [InlineData("<font color=\"#ffff00\">مرحبا.</font>", "<font color=\"#ffff00\">\u202Bمرحبا.\u202C</font>")]
    [InlineData("{\\an8}<font color=\"red\"><i>مرحبا.</i></font>", "{\\an8}<font color=\"red\"><i>\u202Bمرحبا.\u202C</i></font>")]
    [InlineData("<i>مرحبا</i> يا <b>صديقي</b>.", "<i>\u202Bمرحبا</i> يا <b>صديقي</b>.\u202C")]
    public void SrtArabicLineStartingWithMarkup_IsWrappedInsideTheMarkup(string line, string expected)
    {
        var fixedSrt = RtlLineRule.Apply(SrtCue(line), SubtitleTextFormat.SubRip);

        Assert.Equal(SrtCue(expected), fixedSrt);
    }

    [Theory]
    [InlineData("{\\an8}OK.")]
    [InlineData("<font color=\"red\">Hello.</font>")]
    [InlineData("<i>♪</i>")]
    public void SrtLineWithMarkupThatDoesNotStartRightToLeft_StaysTheSame(string line)
    {
        var srt = SrtCue(line);

        Assert.Equal(srt, RtlLineRule.Apply(srt, SubtitleTextFormat.SubRip));
    }

    [Fact]
    public void SrtArabicLineStartingWithLeftToRightMarkReference_StaysTheSame()
    {
        var srt = SrtCue("&lrm;مرحبا.");

        Assert.Equal(srt, RtlLineRule.Apply(srt, SubtitleTextFormat.SubRip));
    }

    [Fact]
    public void SrtLineStartingWithRightToLeftMarkReference_IsWrapped()
    {
        var fixedSrt = RtlLineRule.Apply(SrtCue("&rlm;OK."), SubtitleTextFormat.SubRip);

        Assert.Equal(SrtCue("\u202B&rlm;OK.\u202C"), fixedSrt);
    }

    [Fact]
    public void SrtArabicLineStartingWithAmpersandReference_IsWrapped()
    {
        var fixedSrt = RtlLineRule.Apply(SrtCue("&amp; مرحبا."), SubtitleTextFormat.SubRip);

        Assert.Equal(SrtCue("\u202B&amp; مرحبا.\u202C"), fixedSrt);
    }

    [Fact]
    public void SrtCueWithOneArabicAndOneEnglishLine_WrapsOnlyTheArabicLine()
    {
        var fixedSrt = RtlLineRule.Apply(SrtCue("<i>مرحبا.</i>", "Hello."), SubtitleTextFormat.SubRip);

        Assert.Equal(SrtCue("<i>\u202Bمرحبا.\u202C</i>", "Hello."), fixedSrt);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void SrtLineBreaks_StayAsFound(string lineBreak)
    {
        var srt = SrtSample.Replace("\n", lineBreak, StringComparison.Ordinal);

        var fixedSrt = RtlLineRule.Apply(srt, SubtitleTextFormat.SubRip);

        var expected = string.Join(
            lineBreak,
            "1",
            "00:00:01,000 --> 00:00:03,000",
            "<i>\u202Bمرحبا.\u202C</i>",
            "\u202Bكيف حالك؟\u202C",
            string.Empty,
            "2",
            "00:00:04,000 --> 00:00:05,500",
            "{\\an8}OK.",
            string.Empty,
            string.Empty);
        Assert.Equal(expected, fixedSrt);
    }

    [Fact]
    public void SrtWithByteOrderMark_KeepsIt()
    {
        var fixedSrt = RtlLineRule.Apply("\uFEFF" + SrtCue("مرحبا."), SubtitleTextFormat.SubRip);

        Assert.Equal("\uFEFF" + SrtCue("\u202Bمرحبا.\u202C"), fixedSrt);
    }

    [Fact]
    public void JsonArabicLines_AreWrapped()
    {
        var fixedJson = RtlLineRule.Apply(JsonSample, SubtitleTextFormat.Json);

        Assert.Equal(
            """{"TrackEvents":[{"Id":"1","Text":"\u003Ci\u003E\u202B\u0645\u0631\u062D\u0628\u0627.\u202C\u003C/i\u003E\n\u202B\u0643\u064A\u0641 \u062D\u0627\u0644\u0643\u061F\u202C","StartPositionTicks":10000000,"EndPositionTicks":30000000},{"Id":"2","Text":"{\\an8}OK.","StartPositionTicks":40000000,"EndPositionTicks":55000000}]}""",
            fixedJson);
    }

    [Fact]
    public void JsonCueWithHebrewAndEnglishLines_WrapsOnlyTheHebrewLineAndKeepsItsLineBreak()
    {
        var fixedJson = RtlLineRule.Apply(JsonHebrewSample, SubtitleTextFormat.Json);

        Assert.Equal(
            """{"TrackEvents":[{"Id":"1","Text":"\u202B\u05E9\u05DC\u05D5\u05DD, \u05DE\u05D4 \u05E9\u05DC\u05D5\u05DE\u05DA?\u202C\r\nI\u0027m fine \u0026 you?","StartPositionTicks":10000000,"EndPositionTicks":30000000}]}""",
            fixedJson);
    }

    [Fact]
    public void JsonWithPropertiesJellyfinDoesNotWrite_KeepsThem()
    {
        var fixedJson = RtlLineRule.Apply(JsonWithUnknownPropertiesSample, SubtitleTextFormat.Json);

        Assert.Equal(
            """{"TrackEvents":[{"Id":"1","Text":"\u202B\u0645\u0631\u062D\u0628\u0627.\u202C","StartPositionTicks":10000000,"EndPositionTicks":30000000,"Style":{"Name":"Default","Italic":true}}],"Version":2,"Notes":null}""",
            fixedJson);
    }

    [Fact]
    public void JsonWithNoRtlLine_StaysByteForByteTheSame()
    {
        Assert.Equal(JsonEnglishIndentedSample, RtlLineRule.Apply(JsonEnglishIndentedSample, SubtitleTextFormat.Json));
    }

    [Fact]
    public void JsonWithByteOrderMark_KeepsIt()
    {
        var fixedJson = RtlLineRule.Apply("\uFEFF" + JsonSample, SubtitleTextFormat.Json);

        Assert.StartsWith("\uFEFF{\"TrackEvents\":[{\"Id\":\"1\",\"Text\":\"\\u003Ci\\u003E\\u202B", fixedJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"TrackEvents":[{"Id":"1","Text":"\u0645\u0631""")]
    [InlineData("""{"TrackEvents":[{"Id":"1","Text":"\u0645","Text":"\u0645"}]}""")]
    [InlineData("""[{"Text":"\u0645"}]""")]
    [InlineData("""{"Events":[{"Text":"\u0645"}]}""")]
    [InlineData("")]
    public void JsonThatIsNotAJellyfinSubtitle_ComesBackUnchanged(string json)
    {
        Assert.Equal(json, RtlLineRule.Apply(json, SubtitleTextFormat.Json));
    }

    [Fact]
    public void AssSubtitle_ComesBackUnchanged()
    {
        Assert.Equal(AssSample, RtlLineRule.Apply(AssSample, SubtitleTextFormat.Ass));
    }

    [Fact]
    public void SsaSubtitle_ComesBackUnchanged()
    {
        Assert.Equal(SsaSample, RtlLineRule.Apply(SsaSample, SubtitleTextFormat.Ssa));
    }

    [Theory]
    [MemberData(nameof(EverySample))]
    public void SubtitleFixedTwice_LooksTheSameAsFixedOnce(string subtitle, SubtitleTextFormat format)
    {
        var once = RtlLineRule.Apply(subtitle, format);

        Assert.Equal(once, RtlLineRule.Apply(once, format));
    }

    /// <summary>
    /// Builds one SRT cue the way libse's SubRip writer does, with its lines joined by <c>\n</c>.
    /// </summary>
    private static string SrtCue(params string[] lines)
        => "1\n00:00:01,000 --> 00:00:03,000\n" + string.Join('\n', lines) + "\n\n";
}
