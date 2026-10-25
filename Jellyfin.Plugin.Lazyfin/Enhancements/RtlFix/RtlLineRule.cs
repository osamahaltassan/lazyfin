using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;

/// <summary>
/// Makes every RTL line of a text subtitle lay out right to left, the way mpv shows it.
/// </summary>
/// <remarks>
/// <para>
/// An RTL line is one whose first strong letter is right to left, by the Unicode bidi classes R and AL, such as an
/// Arabic or Hebrew line. Markup such as <c>{\an8}</c> or <c>&lt;i&gt;</c> is skipped when finding that letter, and a
/// character reference such as <c>&amp;rlm;</c> counts as the character it names. The rule judges each line on its own
/// and never looks at a language tag.
/// </para>
/// <para>
/// Depends only on .NET, so it can move into Jellyfin unchanged.
/// </para>
/// </remarks>
public static class RtlLineRule
{
    private const char ByteOrderMark = '\uFEFF';

    /// <summary>
    /// Makes a duplicate property a parse error, so JSON that is not the server's own comes back unchanged.
    /// </summary>
    private static readonly JsonDocumentOptions _strictJson = new() { AllowDuplicateProperties = false };

    /// <summary>
    /// Wraps every RTL line of a subtitle in RLE (U+202B) and PDF (U+202C), and leaves everything else as it was.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>RLE goes right after the markup a line starts with, and PDF right before the markup it ends with, so
    /// <c>{\an8}&lt;i&gt;مرحبا.&lt;/i&gt;</c> becomes <c>{\an8}&lt;i&gt;</c> RLE <c>مرحبا.</c> PDF <c>&lt;/i&gt;</c>.</item>
    /// <item>Lines whose first strong letter is left to right, lines with no strong letter, and everything that is
    /// not a line, such as SRT timings, WebVTT headers and <c>NOTE</c> blocks, or TTML's <c>&lt;head&gt;</c>, stay
    /// byte for byte the same.</item>
    /// <item>Each line keeps the break it had, whether <c>\r\n</c>, <c>\n</c>, or a lone <c>\r</c>, and a byte order
    /// mark at the start is kept.</item>
    /// <item>A line already wrapped is left alone, so applying the rule twice changes nothing.</item>
    /// <item><see cref="SubtitleTextFormat.Ass"/> and <see cref="SubtitleTextFormat.Ssa"/> come back unchanged, as authored.</item>
    /// <item>JSON that cannot be parsed, or that has no <c>TrackEvents</c> array, comes back unchanged.</item>
    /// </list>
    /// </remarks>
    /// <param name="text">The whole subtitle text.</param>
    /// <param name="format">The format of <paramref name="text"/>.</param>
    /// <returns>The subtitle with its RTL lines wrapped, or <paramref name="text"/> itself when nothing changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a defined format.</exception>
    public static string Apply(string text, SubtitleTextFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);

        bool hasByteOrderMark = text.Length > 0 && text[0] == ByteOrderMark;
        var body = hasByteOrderMark ? text[1..] : text;
        var fixedBody = format switch
        {
            SubtitleTextFormat.Json => FixJson(body),
            SubtitleTextFormat.SubRip or SubtitleTextFormat.WebVtt => FixCues(body, format),
            SubtitleTextFormat.Ttml => TtmlLines.Map(body, line => RtlLine.Wrap(line, SubtitleTextFormat.Ttml)),
            SubtitleTextFormat.Ass or SubtitleTextFormat.Ssa => body,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown subtitle text format."),
        };

        if (ReferenceEquals(fixedBody, body))
        {
            return text;
        }

        return hasByteOrderMark ? ByteOrderMark + fixedBody : fixedBody;
    }

    /// <summary>
    /// Wraps the RTL lines of SRT or WebVTT cues: every line after a timing line, up to the blank line that ends the cue.
    /// </summary>
    /// <remarks>
    /// A WebVTT header and its <c>NOTE</c>, <c>STYLE</c>, and <c>REGION</c> blocks never hold a timing line, so they
    /// are never touched, and neither are cue identifiers, which come before it. A timing line that follows a cue with
    /// no blank line between them is read as a cue line, but it never starts right to left, so it stays the same too.
    /// </remarks>
    private static string FixCues(string text, SubtitleTextFormat format)
    {
        bool inCue = false;
        return TextLines.Map(text, line =>
        {
            if (inCue && string.IsNullOrWhiteSpace(line))
            {
                inCue = false;
            }
            else if (inCue)
            {
                return RtlLine.Wrap(line, format);
            }
            else if (line.Contains("-->", StringComparison.Ordinal))
            {
                inCue = true;
            }

            return line;
        });
    }

    /// <summary>
    /// Wraps the RTL lines of each <c>TrackEvents[i].Text</c> in Jellyfin's JSON subtitle.
    /// </summary>
    /// <remarks>
    /// The JSON is parsed and written back, never searched as text, because the server writes non-ASCII letters as
    /// <c>\uXXXX</c> escapes. Every property the rule does not know is kept. When no line changes, the original
    /// text is returned, so its bytes stay the same.
    /// </remarks>
    private static string FixJson(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: _strictJson);
        }
        catch (JsonException)
        {
            return json;
        }

        if (root is not JsonObject subtitle || subtitle["TrackEvents"] is not JsonArray trackEvents)
        {
            return json;
        }

        bool changed = false;
        foreach (var trackEvent in trackEvents)
        {
            if (trackEvent is JsonObject cue && cue["Text"] is JsonValue value && value.TryGetValue<string>(out var cueText))
            {
                var fixedText = TextLines.Map(cueText, line => RtlLine.Wrap(line, SubtitleTextFormat.Json));
                if (!ReferenceEquals(fixedText, cueText))
                {
                    cue["Text"] = fixedText;
                    changed = true;
                }
            }
        }

        return changed ? subtitle.ToJsonString() : json;
    }
}
