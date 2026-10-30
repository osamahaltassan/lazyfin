using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Lazyfin.ServerRoute;
using MediaBrowser.Controller;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.Lazyfin.Tests.ServerRoute;

/// <summary>
/// Checks every action, answer type, argument, and argument property that Lazyfin's registered
/// <see cref="ApiChange"/>s depend on against each Jellyfin build Lazyfin is tested on.
/// </summary>
/// <remarks>
/// <para>
/// The builds are the ones in <c>SERVERS</c> in <c>scripts/dev.sh</c>. The test reads each one's <c>Jellyfin.Api.dll</c>
/// from <c>~/lazyfin-dev/servers/&lt;name&gt;/jellyfin</c>, where <c>scripts/dev.sh setup</c> or <c>scripts/dev.sh api</c>
/// puts it. A missing build fails the test instead of skipping it, so CI never passes without checking.
/// </para>
/// <para>
/// A renamed action, or a changed answer or argument type, compiles fine and stops a change silently. This test is what
/// catches it before a release.
/// </para>
/// </remarks>
public partial class ServerBuildTests
{
    private static readonly string _repo = FindRepo();

    public static TheoryData<string> Servers() => new(ServerNames());

    [Theory]
    [MemberData(nameof(Servers))]
    public void EveryChangedAction_ExistsWithTheTypesTheChangeExpects(string server)
    {
        var api = ReadApi(server);

        var problems = RegisteredChanges().SelectMany(change => Check(api, change)).ToList();

        Assert.True(problems.Count == 0, $"Jellyfin {server}:\n{string.Join('\n', problems)}");
    }

    [Theory]
    [MemberData(nameof(Servers))]
    public void MisspelledAction_IsReported(string server)
    {
        var api = ReadApi(server);
        var misspelled = new ApiChange("Misspelled", [new("TvShows", "GetNextUpp", typeof(QueryResult<BaseItemDto>))]);

        Assert.Equal(["Misspelled: TvShows.GetNextUpp is missing"], Check(api, misspelled));
    }

    [Theory]
    [MemberData(nameof(Servers))]
    public void WrongArgumentType_IsReported(string server)
    {
        var api = ReadApi(server);
        var wrong = new ApiChange("Wrong", [new("TvShows", "GetNextUp", typeof(QueryResult<BaseItemDto>), [ApiArgument.Of<string>("limit")])]);

        Assert.Equal(["Wrong: TvShows.GetNextUp has no argument limit of type System.String"], Check(api, wrong));
    }

    [Fact]
    public void Lazyfin_RegistersApiChanges()
    {
        Assert.NotEmpty(RegisteredChanges());
    }

    private static List<ApiChange> RegisteredChanges()
    {
        var services = new ServiceCollection();
        new PluginServiceRegistrator().RegisterServices(services, Mock.Of<IServerApplicationHost>());
        return services.Select(d => d.ImplementationInstance).OfType<ApiChange>().ToList();
    }

    private static List<string> Check(JellyfinApiMetadata api, ApiChange change)
    {
        var problems = new List<string>();
        foreach (var action in change.Actions)
        {
            var where = $"{change.Name}: {action.Controller}.{action.Action}";
            if (!api.Actions.TryGetValue((action.Controller, action.Action), out var overloads))
            {
                problems.Add($"{where} is missing");
                continue;
            }

            var expected = ApiArgument.TypeName(action.Answer);
            var method = overloads.FirstOrDefault(m => JellyfinApiMetadata.Unwrap(m.ReturnType) == expected);
            if (method is null)
            {
                problems.Add($"{where} answers {string.Join(" or ", overloads.Select(m => m.ReturnType))}, not {expected}");
                continue;
            }

            foreach (var argument in action.Arguments)
            {
                if (!method.Parameters.TryGetValue(argument.Name, out var type) || type != argument.Type)
                {
                    problems.Add($"{where} has no argument {argument.Name} of type {argument.Type}");
                }
                else if (argument.Property is { } property
                    && (!api.Properties.TryGetValue((argument.Type, property), out var propertyType) || propertyType != argument.PropertyType))
                {
                    problems.Add($"{where}: {argument.Type} has no property {property} of type {argument.PropertyType}");
                }
            }
        }

        return problems;
    }

    private static JellyfinApiMetadata ReadApi(string server)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "lazyfin-dev", "servers", server, "jellyfin", "Jellyfin.Api.dll");
        Assert.True(File.Exists(path), $"Jellyfin {server} is missing at {path}. Run scripts/dev.sh api.");
        return JellyfinApiMetadata.Read(path);
    }

    private static IEnumerable<string> ServerNames()
    {
        var script = File.ReadAllText(Path.Combine(_repo, "scripts", "dev.sh"));
        var servers = Regex.Match(script, @"^SERVERS=\((.*?)^\)", RegexOptions.Multiline | RegexOptions.Singleline).Groups[1].Value;
        return ServerLine().Matches(servers).Select(m => m.Groups[1].Value);
    }

    private static string FindRepo()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "Lazyfin.slnx")))
        {
            repo = repo.Parent;
        }

        return repo?.FullName ?? throw new InvalidOperationException("The test runs outside the Lazyfin repository.");
    }

    [GeneratedRegex("^\\s*\"([^|\"]+)\\|", RegexOptions.Multiline)]
    private static partial Regex ServerLine();
}
