using System;
using Jellyfin.Plugin.Lazyfin.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Lazyfin;

/// <summary>
/// The Lazyfin plugin, as the Jellyfin server sees it.
/// </summary>
/// <remarks>
/// Lazyfin has no admin settings and no configuration page. Each enhancement is on as soon as Lazyfin is installed.
/// <see cref="PluginServiceRegistrator"/> is where enhancements plug into the server.
/// </remarks>
public class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>
    /// The plugin id. It must match the <c>guid</c> in <c>build.yaml</c>, and it never changes.
    /// </summary>
    public const string PluginId = "e8fe2245-b033-4a98-9d02-f035bd7044bf";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class. The Jellyfin server calls this.
    /// </summary>
    /// <param name="applicationPaths">The server's application paths.</param>
    /// <param name="xmlSerializer">The server's XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    /// <inheritdoc />
    public override string Name => "Lazyfin";

    /// <inheritdoc />
    public override string Description => "Adds a few small changes to how Jellyfin behaves for viewers, with nothing to set up.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse(PluginId);
}
