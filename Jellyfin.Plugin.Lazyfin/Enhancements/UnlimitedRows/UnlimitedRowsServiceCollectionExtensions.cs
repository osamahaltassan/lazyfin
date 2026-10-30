using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Lazyfin.ServerRoute;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.UnlimitedRows;

/// <summary>
/// Turns the unlimited Continue Watching and Next Up on in the Jellyfin server.
/// </summary>
public static class UnlimitedRowsServiceCollectionExtensions
{
    /// <summary>
    /// Changes Jellyfin's Next Up and resume actions, legacy ones included, the way <see cref="UnlimitedRows"/> says,
    /// for every client.
    /// </summary>
    /// <remarks>
    /// Jellyfin builds a fresh answer for each of these actions, but the change returns a new page anyway, so it never
    /// edits Jellyfin's answer in place.
    /// </remarks>
    /// <param name="services">The server's services.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddUnlimitedRows(this IServiceCollection services)
    {
        var startIndex = ApiArgument.Of<int?>(UnlimitedRows.StartIndex);
        var limit = ApiArgument.Of<int?>(UnlimitedRows.Limit);
        var emptyLaterPage = ApiChange.Answer<QueryResult<BaseItemDto>>((answer, _) => UnlimitedRows.EmptyLaterPage(answer));

        return services
            .AddApiChange(new ApiChange(
                "NextUpUnlimited",
                [new("TvShows", "GetNextUp", typeof(QueryResult<BaseItemDto>), [startIndex, limit, ApiArgument.Of<DateTime?>(UnlimitedRows.NextUpDateCutoff)])])
            {
                ChangeArguments = (arguments, _) => UnlimitedRows.NextUpArguments(StartIndexOf(arguments)),
                ChangeAnswer = emptyLaterPage,
            })
            .AddApiChange(new ApiChange(
                "ContinueWatchingUnlimited",
                [
                    new("Items", "GetResumeItems", typeof(QueryResult<BaseItemDto>), [startIndex, limit]),
                    new("Items", "GetResumeItemsLegacy", typeof(QueryResult<BaseItemDto>), [startIndex, limit]),
                ])
            {
                ChangeArguments = (arguments, _) => UnlimitedRows.ContinueWatchingArguments(StartIndexOf(arguments)),
                ChangeAnswer = emptyLaterPage,
            });
    }

    private static int? StartIndexOf(IReadOnlyDictionary<string, object?> arguments)
        => arguments.TryGetValue(UnlimitedRows.StartIndex, out var value) ? value as int? : null;
}
