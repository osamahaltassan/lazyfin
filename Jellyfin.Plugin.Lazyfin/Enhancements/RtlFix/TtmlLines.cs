using System;
using System.Text;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;

/// <summary>
/// Rewrites the cue lines of a TTML document and keeps every other character as it was.
/// </summary>
/// <remarks>
/// A cue is a <c>&lt;p&gt;</c> element, and its lines are the runs of its content split by <c>&lt;br /&gt;</c>, in any
/// spelling such as <c>&lt;br/&gt;</c> or <c>&lt;br&gt;</c>. Raw line breaks inside a <c>&lt;p&gt;</c> are XML layout,
/// not lines, so a run may span several of them. The document is scanned as text, never parsed and written back, so
/// its bytes outside the changed runs stay the same. Element names carry no namespace prefix, the way the server's
/// TTML writer spells them.
/// </remarks>
internal static class TtmlLines
{
    /// <summary>
    /// Passes each line of each <c>&lt;p&gt;</c> in <paramref name="ttml"/> through <paramref name="map"/>, in order.
    /// </summary>
    /// <param name="ttml">The TTML document to rewrite.</param>
    /// <param name="map">Returns the new content of one line, or the line itself to keep it.</param>
    /// <returns>The rewritten document, or <paramref name="ttml"/> itself when no line changed.</returns>
    public static string Map(string ttml, Func<string, string> map)
    {
        StringBuilder? output = null;
        int copied = 0;
        int index = 0;
        while (TryFindTag(ttml, "p", index, ttml.Length, out _, out int tagEnd))
        {
            index = tagEnd;
            if (ttml[tagEnd - 2] == '/' || !TryFindClosingTag(ttml, "p", tagEnd, out int contentEnd))
            {
                continue;
            }

            int lineStart = tagEnd;
            while (lineStart <= contentEnd)
            {
                int lineEnd = contentEnd;
                int nextLine = contentEnd + 1;
                if (TryFindTag(ttml, "br", lineStart, contentEnd, out int breakStart, out int breakEnd))
                {
                    lineEnd = breakStart;
                    nextLine = breakEnd;
                }

                var line = ttml[lineStart..lineEnd];
                var mapped = map(line);
                if (!string.Equals(mapped, line, StringComparison.Ordinal))
                {
                    output ??= new StringBuilder(ttml.Length + 16);
                    output.Append(ttml, copied, lineStart - copied).Append(mapped);
                    copied = lineEnd;
                }

                lineStart = nextLine;
            }

            index = contentEnd;
        }

        return output?.Append(ttml, copied, ttml.Length - copied).ToString() ?? ttml;
    }

    /// <summary>
    /// Finds the next start tag named <paramref name="name"/> that opens at or after <paramref name="from"/> and before
    /// <paramref name="to"/>, and gives the index of its <c>&lt;</c> and the index right after its <c>&gt;</c>.
    /// </summary>
    private static bool TryFindTag(string ttml, string name, int from, int to, out int tagStart, out int tagEnd)
    {
        tagEnd = 0;
        tagStart = from;
        while ((tagStart = ttml.IndexOf("<" + name, tagStart, to - tagStart, StringComparison.Ordinal)) >= 0)
        {
            int afterName = tagStart + name.Length + 1;
            if (afterName < ttml.Length && ttml[afterName] is '>' or '/' or ' ' or '\t' or '\r' or '\n')
            {
                int close = ttml.IndexOf('>', afterName);
                if (close < 0)
                {
                    return false;
                }

                tagEnd = close + 1;
                return true;
            }

            tagStart = afterName;
        }

        return false;
    }

    /// <summary>
    /// Finds the next end tag named <paramref name="name"/> at or after <paramref name="from"/>, and gives the index of
    /// its <c>&lt;</c>.
    /// </summary>
    private static bool TryFindClosingTag(string ttml, string name, int from, out int tagStart)
    {
        tagStart = from;
        while ((tagStart = ttml.IndexOf("</" + name, tagStart, StringComparison.Ordinal)) >= 0)
        {
            int afterName = tagStart + name.Length + 2;
            if (afterName < ttml.Length && ttml[afterName] is '>' or ' ' or '\t' or '\r' or '\n')
            {
                return true;
            }

            tagStart = afterName;
        }

        return false;
    }
}
