using Jellyfin.Data.Events.Users;
using MediaBrowser.Controller.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.WatchedInLatest;

/// <summary>
/// Turns on showing watched items in Latest Media for new viewers in the Jellyfin server.
/// </summary>
public static class WatchedInLatestServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ShowWatchedInLatestForNewViewers"/> for Jellyfin's user-created event, the way Jellyfin
    /// registers its own event consumers.
    /// </summary>
    /// <param name="services">The server's services.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddWatchedInLatest(this IServiceCollection services)
        => services.AddScoped<IEventConsumer<UserCreatedEventArgs>, ShowWatchedInLatestForNewViewers>();
}
