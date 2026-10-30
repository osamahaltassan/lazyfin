using Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;
using Jellyfin.Plugin.Lazyfin.Enhancements.UnlimitedRows;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Lazyfin;

/// <summary>
/// Registers every Lazyfin enhancement with the Jellyfin server's services.
/// </summary>
/// <remarks>
/// Each enhancement lives in its own folder under <c>Enhancements/</c>, with its own namespace,
/// and exposes one <c>Add&lt;Enhancement&gt;(this IServiceCollection)</c> extension method.
/// Adding an enhancement means adding one call to that method in <see cref="RegisterServices"/>.
/// Removing that call turns the enhancement off completely.
/// </remarks>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddRtlFix();
        serviceCollection.AddUnlimitedRows();
    }
}
