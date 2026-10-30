using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Jellyfin.Plugin.Lazyfin.ServerRoute;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Lazyfin.Tests.ServerRoute;

/// <summary>
/// Checks how <see cref="ServerRouteFilter"/> applies changes to an action's request and answer, and that it fails open.
/// </summary>
public class ServerRouteFilterTests
{
    private static readonly ApiAction _listed = new("Things", "GetThings", typeof(List<string>), [ApiArgument.Of<int?>("limit")]);

    [Fact]
    public async Task ArgumentsChange_WritesTheValuesItReturns()
    {
        var run = NewRun();
        run.Arguments["limit"] = 12;

        await run.RunAsync(Change(arguments: (_, _) => new Dictionary<string, object?> { ["limit"] = 50 }));

        Assert.Equal(50, run.ArgumentsSeen!["limit"]);
    }

    [Fact]
    public async Task ArgumentsChange_ThatThrows_LeavesTheRequestAsSent()
    {
        var run = NewRun();
        run.Arguments["limit"] = 12;

        await run.RunAsync(Change(arguments: (_, _) => throw new InvalidOperationException("broken")));

        Assert.Equal(12, run.ArgumentsSeen!["limit"]);
    }

    [Fact]
    public async Task ArgumentsChange_ThatWritesAnUnlistedArgument_WritesNothing()
    {
        var run = NewRun();
        run.Arguments["limit"] = 12;

        await run.RunAsync(Change(arguments: (_, _) => new Dictionary<string, object?> { ["limit"] = 50, ["startIndex"] = 3 }));

        Assert.Equal(12, run.ArgumentsSeen!["limit"]);
        Assert.False(run.ArgumentsSeen.ContainsKey("startIndex"));
    }

    [Fact]
    public async Task ArgumentsChange_ThatWritesTheWrongType_WritesNothing()
    {
        var run = NewRun();
        run.Arguments["limit"] = 12;

        await run.RunAsync(Change(arguments: (_, _) => new Dictionary<string, object?> { ["limit"] = "fifty" }));

        Assert.Equal(12, run.ArgumentsSeen!["limit"]);
    }

    [Fact]
    public async Task ArgumentsChange_SeesTheRequestContext()
    {
        var run = NewRun();
        var viewer = Guid.NewGuid();
        run.Claims.Add(new Claim("Jellyfin-UserId", viewer.ToString()));
        run.Claims.Add(new Claim("Jellyfin-Client", "Jellyfin Web"));
        run.Claims.Add(new Claim("Jellyfin-DeviceId", "device-1"));
        run.UserAgent = "Safari";
        ApiRequest? seen = null;

        await run.RunAsync(Change(arguments: (_, request) =>
        {
            seen = request;
            return new Dictionary<string, object?>();
        }));

        Assert.Equal(new ApiRequest(viewer, "Jellyfin Web", "device-1", "Safari"), seen);
    }

    [Fact]
    public async Task AnswerChange_ReplacesTheAnswer()
    {
        var run = NewRun();
        run.Result = new ObjectResult(new List<string> { "a" });

        var answer = await run.RunAsync(Change(answer: (a, _) => [.. a, "b"]));

        Assert.Equal<string>(["a", "b"], Assert.IsType<List<string>>(answer));
    }

    [Fact]
    public async Task AnswerChanges_RunInRegistrationOrder_EachSeeingThePreviousOutput()
    {
        var run = NewRun();
        run.Result = new ObjectResult(new List<string> { "a" });

        var answer = await run.RunAsync(Change(answer: (a, _) => [.. a, "b"]), Change(answer: (a, _) => [.. a, "c"]));

        Assert.Equal<string>(["a", "b", "c"], Assert.IsType<List<string>>(answer));
    }

    [Fact]
    public async Task AnswerChange_ThatThrows_PassesJellyfinsAnswerThrough()
    {
        var run = NewRun();
        var jellyfins = new List<string> { "a" };
        run.Result = new ObjectResult(jellyfins);

        var answer = await run.RunAsync(Change(answer: (_, _) => throw new InvalidOperationException("broken")));

        Assert.Same(jellyfins, answer);
    }

    [Fact]
    public async Task AnswerChange_OnAnUnlistedType_IsSkipped()
    {
        var run = NewRun();
        run.Result = new ObjectResult("not a list");

        var answer = await run.RunAsync(Change(answer: (a, _) => [.. a, "b"]));

        Assert.Equal("not a list", answer);
    }

    [Fact]
    public async Task AnswerChange_OnAnErrorAnswer_IsSkipped()
    {
        var run = NewRun();
        var problem = new List<string> { "problem" };
        run.Result = new ObjectResult(problem) { StatusCode = 400 };

        var answer = await run.RunAsync(Change(answer: (a, _) => [.. a, "b"]));

        Assert.Same(problem, answer);
    }

    [Fact]
    public async Task Changes_SkipOtherActions()
    {
        var run = new ActionRun("Things", "GetOtherThings", ("limit", typeof(int?)));
        run.Arguments["limit"] = 12;
        var jellyfins = new List<string> { "a" };
        run.Result = new ObjectResult(jellyfins);

        var answer = await run.RunAsync(Change(
            arguments: (_, _) => new Dictionary<string, object?> { ["limit"] = 50 },
            answer: (a, _) => [.. a, "b"]));

        Assert.Equal(12, run.ArgumentsSeen!["limit"]);
        Assert.Same(jellyfins, answer);
    }

    private static ActionRun NewRun() => new("Things", "GetThings", ("limit", typeof(int?)), ("startIndex", typeof(int?)));

    private static ApiChange Change(ArgumentsChange? arguments = null, Func<List<string>, ApiRequest, List<string>>? answer = null)
        => new("Test", [_listed])
        {
            ChangeArguments = arguments,
            ChangeAnswer = answer is null ? null : ApiChange.Answer(answer),
        };
}
