using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Lazyfin.Enhancements.WatchedInLatest;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.Lazyfin.Tests.Enhancements.WatchedInLatest;

/// <summary>
/// Checks what happens to a new viewer after <see cref="WatchedInLatestServiceCollectionExtensions.AddWatchedInLatest"/>,
/// when Jellyfin publishes its user-created event the way its <c>EventManager</c> does.
/// </summary>
public class AddWatchedInLatestTests
{
    [Fact]
    public async Task NewViewer_ShowsWatchedItemsInLatest()
    {
        var userManager = new Mock<IUserManager>();
        var viewer = new User("viewer", "auth", "reset");

        await PublishUserCreatedAsync(userManager.Object, viewer);

        Assert.False(viewer.HidePlayedInLatest);
        userManager.Verify(m => m.UpdateUserAsync(viewer), Times.Once);
    }

    [Fact]
    public async Task NewViewer_AlreadyShowingWatchedItems_IsNotSavedAgain()
    {
        var userManager = new Mock<IUserManager>();
        var viewer = new User("viewer", "auth", "reset") { HidePlayedInLatest = false };

        await PublishUserCreatedAsync(userManager.Object, viewer);

        userManager.Verify(m => m.UpdateUserAsync(It.IsAny<User>()), Times.Never);
    }

    private static async Task PublishUserCreatedAsync(IUserManager userManager, User viewer)
    {
        await using var services = new ServiceCollection()
            .AddSingleton(userManager)
            .AddWatchedInLatest()
            .BuildServiceProvider();
        using var scope = services.CreateScope();
        var consumers = scope.ServiceProvider.GetServices<IEventConsumer<UserCreatedEventArgs>>().ToList();

        Assert.Single(consumers);
        await consumers[0].OnEvent(new UserCreatedEventArgs(viewer));
    }
}
