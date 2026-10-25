using System;
using System.Buffers;
using System.Net;
using System.Text;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;

/// <summary>
/// Wraps one subtitle line in RLE and PDF when its first strong letter is right to left.
/// </summary>
/// <remarks>
/// <para>
/// A line carries the markup of its format, and markup is never read for letters:
/// </para>
/// <list type="bullet">
/// <item>SRT and JSON: <c>{\...}</c> override blocks such as <c>{\an8}</c>, and HTML-style tags such as
/// <c>&lt;i&gt;</c> or <c>&lt;font color="red"&gt;</c>.</item>
/// <item>WebVTT: every cue tag, such as <c>&lt;v Bob&gt;</c>, <c>&lt;c.yellow&gt;</c>, or the timestamp tag
/// <c>&lt;00:00:01.000&gt;</c>.</item>
/// <item>TTML: every tag, such as <c>&lt;span tts:fontStyle="italic"&gt;</c>, and whitespace, which XML lays out
/// rather than shows.</item>
/// </list>
/// <para>
/// A character reference such as <c>&amp;rlm;</c> counts as the character it names. RLE goes right after the markup a
/// line starts with, and PDF right before the markup it ends with, so tags such as <c>{\an8}</c> stay first.
/// </para>
/// </remarks>
internal static class RtlLine
{
    private const char RightToLeftEmbedding = '\u202B';
    private const char PopDirectionalFormatting = '\u202C';

    /// <summary>
    /// The longest name or number a character reference may have, so a stray <c>&amp;</c> is not read to the end of a long line.
    /// </summary>
    private const int MaxReferenceLength = 32;

    /// <summary>
    /// Wraps a line in RLE and PDF when it is an RTL line, or returns it as it was.
    /// </summary>
    /// <param name="line">One line, without its line break.</param>
    /// <param name="format">The format the line comes from, which says what markup it carries.</param>
    /// <returns>The wrapped line, or <paramref name="line"/> itself.</returns>
    public static string Wrap(string line, SubtitleTextFormat format)
    {
        int textStart = 0;
        int markupLength;
        while ((markupLength = MarkupLength(line, textStart, format)) > 0)
        {
            textStart += markupLength;
        }

        int textEnd = textStart;
        bool? rightToLeft = null;
        int index = textStart;
        while (index < line.Length)
        {
            markupLength = MarkupLength(line, index, format);
            if (markupLength > 0)
            {
                index += markupLength;
                continue;
            }

            if (!TryDecodeReference(line, index, out var rune, out int runeLength))
            {
                Rune.DecodeFromUtf16(line.AsSpan(index), out rune, out runeLength);
            }

            if (rightToLeft is null && StrongDirection.IsRightToLeft(rune))
            {
                rightToLeft = true;
            }
            else if (rightToLeft is null && StrongDirection.IsLeftToRight(rune))
            {
                rightToLeft = false;
            }

            index += runeLength;
            textEnd = index;
        }

        bool wrapped = textEnd - textStart >= 2 && line[textStart] == RightToLeftEmbedding && line[textEnd - 1] == PopDirectionalFormatting;
        if (rightToLeft != true || wrapped)
        {
            return line;
        }

        return $"{line[..textStart]}{RightToLeftEmbedding}{line[textStart..textEnd]}{PopDirectionalFormatting}{line[textEnd..]}";
    }

    /// <summary>
    /// Measures the markup of <paramref name="format"/> that starts at <paramref name="index"/>.
    /// </summary>
    /// <remarks>
    /// In WebVTT and TTML every <c>&lt;</c> opens a tag, because a <c>&lt;</c> in the text must be written as
    /// <c>&amp;lt;</c>. In SRT and JSON only <c>&lt;</c> followed by a letter or <c>/</c> does.
    /// </remarks>
    /// <returns>The length of the markup, or 0 when none starts there.</returns>
    private static int MarkupLength(string line, int index, SubtitleTextFormat format)
    {
        if (format == SubtitleTextFormat.Ttml && IsXmlWhitespace(line[index]))
        {
            int end = index + 1;
            while (end < line.Length && IsXmlWhitespace(line[end]))
            {
                end++;
            }

            return end - index;
        }

        if (index + 1 >= line.Length)
        {
            return 0;
        }

        bool everyAngleBracketIsATag = format is SubtitleTextFormat.WebVtt or SubtitleTextFormat.Ttml;
        char next = line[index + 1];
        int close = -1;
        if (!everyAngleBracketIsATag && line[index] == '{' && next == '\\')
        {
            close = line.IndexOf('}', index + 2);
        }
        else if (line[index] == '<' && (everyAngleBracketIsATag || next == '/' || char.IsAsciiLetter(next)))
        {
            close = line.IndexOf('>', index + 1);
        }

        return close < 0 ? 0 : close - index + 1;
    }

    private static bool IsXmlWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n';

    /// <summary>
    /// Reads the character reference that starts at <paramref name="index"/>, such as <c>&amp;rlm;</c> or <c>&amp;#1605;</c>,
    /// as the one character it names.
    /// </summary>
    /// <returns><c>true</c> when a reference to one known character starts there.</returns>
    private static bool TryDecodeReference(string line, int index, out Rune rune, out int length)
    {
        rune = default;
        length = 0;
        if (line[index] != '&')
        {
            return false;
        }

        int end = index + 1;
        while (end < line.Length && end - index <= MaxReferenceLength && (char.IsAsciiLetterOrDigit(line[end]) || line[end] == '#'))
        {
            end++;
        }

        if (end >= line.Length || line[end] != ';' || end == index + 1)
        {
            return false;
        }

        var reference = line[index..(end + 1)];
        var decoded = WebUtility.HtmlDecode(reference);
        if (decoded == reference || Rune.DecodeFromUtf16(decoded, out rune, out int decodedLength) != OperationStatus.Done || decodedLength != decoded.Length)
        {
            return false;
        }

        length = reference.Length;
        return true;
    }
}
