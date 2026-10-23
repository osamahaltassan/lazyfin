using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Lazyfin.Configuration;

/// <summary>
/// Lazyfin's saved settings. It is empty, because Lazyfin has no admin settings.
/// </summary>
/// <remarks>
/// Jellyfin only loads plugins that derive from <c>BasePlugin&lt;TConfiguration&gt;</c>, so this class exists even with nothing in it.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
}
