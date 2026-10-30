using System.Collections.Generic;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.UnlimitedRows;

/// <summary>
/// Lifts the limits a client puts on Next Up and on the resume rows, such as Continue Watching, Continue Listening, and
/// Continue Reading, so they list everything a viewer started.
/// </summary>
/// <remarks>
/// <para>
/// A client asks for a first page, with no start index or 0, and gets every item, whatever limit it asked for. Next Up
/// also drops the client's date cutoff, so a show the viewer has not touched for years still shows.
/// </para>
/// <para>
/// A client that pages on, with a start index above 0, gets an empty page, because the first page already held every
/// item. Without that, a client that pages would show the items after its first page twice.
/// </para>
/// </remarks>
public static class UnlimitedRowsRules
{
    /// <summary>
    /// The start index argument of Jellyfin's Next Up and resume actions.
    /// </summary>
    public const string StartIndex = "startIndex";

    /// <summary>
    /// The limit argument of Jellyfin's Next Up and resume actions.
    /// </summary>
    public const string Limit = "limit";

    /// <summary>
    /// The date cutoff argument of Jellyfin's Next Up action. Jellyfin reads a missing cutoff as "no cutoff".
    /// </summary>
    public const string NextUpDateCutoff = "nextUpDateCutoff";

    /// <summary>
    /// Gets the arguments to clear on a Continue Watching request.
    /// </summary>
    /// <param name="startIndex">The start index the client asked for.</param>
    /// <returns>The limit, cleared, on a first page. Nothing on a later page.</returns>
    public static IReadOnlyDictionary<string, object?> ContinueWatchingArguments(int? startIndex)
        => IsFirstPage(startIndex) ? new Dictionary<string, object?> { [Limit] = null } : new Dictionary<string, object?>();

    /// <summary>
    /// Gets the arguments to clear on a Next Up request.
    /// </summary>
    /// <param name="startIndex">The start index the client asked for.</param>
    /// <returns>The date cutoff, cleared, and on a first page the limit too.</returns>
    public static IReadOnlyDictionary<string, object?> NextUpArguments(int? startIndex)
        => IsFirstPage(startIndex)
            ? new Dictionary<string, object?> { [Limit] = null, [NextUpDateCutoff] = null }
            : new Dictionary<string, object?> { [NextUpDateCutoff] = null };

    /// <summary>
    /// Empties a later page, whose items the first page already held.
    /// </summary>
    /// <param name="answer">Jellyfin's answer.</param>
    /// <returns><paramref name="answer"/> on a first page, or a new empty page with the same start index and total.</returns>
    public static QueryResult<BaseItemDto> EmptyLaterPage(QueryResult<BaseItemDto> answer)
        => IsFirstPage(answer.StartIndex) ? answer : new QueryResult<BaseItemDto>(answer.StartIndex, answer.TotalRecordCount, []);

    private static bool IsFirstPage(int? startIndex) => startIndex is null or <= 0;
}
