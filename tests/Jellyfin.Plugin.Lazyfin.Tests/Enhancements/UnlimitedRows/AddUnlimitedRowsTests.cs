using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Lazyfin.Enhancements.UnlimitedRows;
using Jellyfin.Plugin.Lazyfin.ServerRoute;
using Jellyfin.Plugin.Lazyfin.Tests.ServerRoute;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.Lazyfin.Tests.Enhancements.UnlimitedRows;

/// <summary>
/// Checks what Jellyfin's Next Up and resume actions run with and answer after
/// <see cref="UnlimitedRowsServiceCollectionExtensions.AddUnlimitedRows"/>.
/// </summary>
/// <remarks>
/// Each test sends the arguments Jellyfin Web's home screen sends, such as <c>limit=24</c>, through
/// <see cref="ServerRouteFilter"/> with the registered changes.
/// </remarks>
public class AddUnlimitedRowsTests
{
    private static readonly ApiChange[] _changes = new ServiceCollection()
        .AddUnlimitedRows()
        .Select(d => d.ImplementationInstance)
        .OfType<ApiChange>()
        .ToArray();

    [Fact]
    public async Task NextUp_FirstPage_RunsWithNoLimitAndNoCutoff()
    {
        var run = NextUp();
        run.Arguments["limit"] = 24;
        run.Arguments["nextUpDateCutoff"] = new DateTime(2025, 10, 30, 0, 0, 0, DateTimeKind.Utc);

        await run.RunAsync(_changes);

        Assert.Null(run.ArgumentsSeen!["limit"]);
        Assert.Null(run.ArgumentsSeen["nextUpDateCutoff"]);
    }

    [Fact]
    public async Task NextUp_LaterPage_KeepsItsLimitAndAnswersEmpty()
    {
        var run = NextUp();
        run.Arguments["startIndex"] = 24;
        run.Arguments["limit"] = 24;
        run.Result = new ObjectResult(Page(24, 100, 24));

        var answer = Assert.IsType<QueryResult<BaseItemDto>>(await run.RunAsync(_changes));

        Assert.Equal(24, run.ArgumentsSeen!["limit"]);
        Assert.Null(run.ArgumentsSeen["nextUpDateCutoff"]);
        Assert.Empty(answer.Items);
        Assert.Equal(24, answer.StartIndex);
        Assert.Equal(100, answer.TotalRecordCount);
    }

    [Fact]
    public async Task NextUp_FirstPage_AnswersEveryItem()
    {
        var run = NextUp();
        var jellyfins = Page(0, 100, 100);
        run.Result = new ObjectResult(jellyfins);

        var answer = await run.RunAsync(_changes);

        Assert.Same(jellyfins, answer);
    }

    [Theory]
    [InlineData("GetResumeItems")]
    [InlineData("GetResumeItemsLegacy")]
    public async Task ContinueWatching_FirstPage_RunsWithNoLimit(string action)
    {
        var run = Resume(action);
        run.Arguments["startIndex"] = 0;
        run.Arguments["limit"] = 12;

        await run.RunAsync(_changes);

        Assert.Null(run.ArgumentsSeen!["limit"]);
    }

    [Theory]
    [InlineData("GetResumeItems")]
    [InlineData("GetResumeItemsLegacy")]
    public async Task ContinueWatching_LaterPage_AnswersEmpty(string action)
    {
        var run = Resume(action);
        run.Arguments["startIndex"] = 12;
        run.Arguments["limit"] = 12;
        run.Result = new ObjectResult(Page(12, 30, 12));

        var answer = Assert.IsType<QueryResult<BaseItemDto>>(await run.RunAsync(_changes));

        Assert.Equal(12, run.ArgumentsSeen!["limit"]);
        Assert.Empty(answer.Items);
    }

    private static ActionRun NextUp()
        => new("TvShows", "GetNextUp", ("startIndex", typeof(int?)), ("limit", typeof(int?)), ("nextUpDateCutoff", typeof(DateTime?)));

    private static ActionRun Resume(string action)
        => new("Items", action, ("startIndex", typeof(int?)), ("limit", typeof(int?)));

    private static QueryResult<BaseItemDto> Page(int startIndex, int total, int count)
        => new(startIndex, total, Enumerable.Range(startIndex, count).Select(i => new BaseItemDto { Name = $"Item {i}" }).ToArray());
}
