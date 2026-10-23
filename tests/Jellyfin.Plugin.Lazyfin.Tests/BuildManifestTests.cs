using System;
using System.IO;
using System.Text.RegularExpressions;
using MediaBrowser.Controller.Plugins;
using Xunit;

namespace Jellyfin.Plugin.Lazyfin.Tests;

/// <summary>
/// Checks that <c>build.yaml</c>, which becomes the plugin repository manifest entry, agrees with the compiled plugin.
/// </summary>
public partial class BuildManifestTests
{
    private static readonly string _buildYaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "build.yaml"));

    [Fact]
    public void Guid_MatchesPluginId()
    {
        Assert.Equal(Guid.Parse(Plugin.PluginId), Guid.Parse(ReadField("guid")));
    }

    [Fact]
    public void TargetAbi_MatchesReferencedJellyfinVersion()
    {
        var referenced = typeof(IPluginServiceRegistrator).Assembly.GetName().Version;

        Assert.Equal(referenced, Version.Parse(ReadField("targetAbi")));
    }

    private static string ReadField(string name)
    {
        var match = FieldRegex().Match(_buildYaml);
        while (match.Success && match.Groups["name"].Value != name)
        {
            match = match.NextMatch();
        }

        Assert.True(match.Success, $"build.yaml has no {name} field");
        return match.Groups["value"].Value;
    }

    [GeneratedRegex("^(?<name>[A-Za-z]+): \"(?<value>[^\"]*)\"$", RegexOptions.Multiline)]
    private static partial Regex FieldRegex();
}
