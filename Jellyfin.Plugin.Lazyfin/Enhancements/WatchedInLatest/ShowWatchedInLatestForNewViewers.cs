using System;
using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.WatchedInLatest;

/// <summary>
/// Turns off "Hide watched content from Latest Media" for each viewer created while Lazyfin is installed, so watched
/// items stay in the home screen's Latest rows.
/// </summary>
/// <remarks>
/// <para>
/// Jellyfin turns the setting on for every new viewer. It is stored on the server, in the viewer's configuration, so
/// every client that shows Latest rows follows it.
/// </para>
/// <para>
/// Viewers who existed before Lazyfin was installed keep their setting, because Jellyfin stores it as a plain true or
/// false and Lazyfin cannot tell the default from a viewer's own choice. A new viewer can still turn it back on.
/// </para>
/// <para>
/// Jellyfin creates the first admin in its startup wizard without a user-created event, so that admin keeps Jellyfin's
/// default. Jellyfin catches and logs anything this throws, and the viewer is created either way.
/// </para>
/// </remarks>
public sealed class ShowWatchedInLatestForNewViewers : IEventConsumer<UserCreatedEventArgs>
{
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowWatchedInLatestForNewViewers"/> class.
    /// </summary>
    /// <param name="userManager">The server's user manager.</param>
    public ShowWatchedInLatestForNewViewers(IUserManager userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task OnEvent(UserCreatedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var viewer = eventArgs.Argument;
        if (!viewer.HidePlayedInLatest)
        {
            return;
        }

        viewer.HidePlayedInLatest = false;
        await _userManager.UpdateUserAsync(viewer).ConfigureAwait(false);
    }
}
