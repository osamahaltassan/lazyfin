using System;
using System.Text;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;

/// <summary>
/// Rewrites a text line by line and keeps every line break as it was found.
/// </summary>
internal static class TextLines
{
    /// <summary>
    /// Passes each line of <paramref name="text"/> through <paramref name="map"/>, in order.
    /// </summary>
    /// <remarks>
    /// A line ends at <c>\r\n</c>, <c>\n</c>, or a lone <c>\r</c>, and <paramref name="map"/> never sees the break.
    /// Each break is written back exactly as found, so text that mixes breaks keeps its mix.
    /// </remarks>
    /// <param name="text">The text to rewrite.</param>
    /// <param name="map">Returns the new content of one line, or the line itself to keep it.</param>
    /// <returns>The rewritten text, or <paramref name="text"/> itself when no line changed.</returns>
    public static string Map(string text, Func<string, string> map)
    {
        StringBuilder? output = null;
        int start = 0;
        while (start < text.Length)
        {
            int end = text.AsSpan(start).IndexOfAny('\r', '\n');
            end = end < 0 ? text.Length : start + end;
            int next = end;
            if (next < text.Length && text[next] == '\r')
            {
                next++;
            }

            if (next < text.Length && text[next] == '\n')
            {
                next++;
            }

            var line = text[start..end];
            var mapped = map(line);
            if (output is null && !string.Equals(mapped, line, StringComparison.Ordinal))
            {
                output = new StringBuilder(text.Length + 16).Append(text, 0, start);
            }

            output?.Append(mapped).Append(text, end, next - end);
            start = next;
        }

        return output?.ToString() ?? text;
    }
}
