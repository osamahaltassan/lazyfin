using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Lazyfin.ServerRoute;

/// <summary>
/// At server start, warns once about each <see cref="ApiAction"/> the running server does not have.
/// </summary>
/// <remarks>
/// A renamed action otherwise stops its change without a sound. The server-build test catches that for every version
/// Lazyfin is tested on; this warning is the fallback for the versions it is not. It runs as a hosted service, once MVC
/// has built its action list.
/// </remarks>
public sealed class MissingApiActionCheck : IHostedService
{
    private readonly IActionDescriptorCollectionProvider _actions;
    private readonly IEnumerable<ApiChange> _changes;
    private readonly ILogger<MissingApiActionCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingApiActionCheck"/> class.
    /// </summary>
    /// <param name="actions">MVC's action list.</param>
    /// <param name="changes">Every registered change.</param>
    /// <param name="logger">The logger.</param>
    public MissingApiActionCheck(IActionDescriptorCollectionProvider actions, IEnumerable<ApiChange> changes, ILogger<MissingApiActionCheck> logger)
    {
        _actions = actions;
        _changes = changes;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var present = _actions.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(action => (action.ControllerName, action.ActionName))
            .ToHashSet();
        foreach (var change in _changes)
        {
            foreach (var action in change.Actions.Where(action => !present.Contains((action.Controller, action.Action))))
            {
                _logger.LogWarning(
                    "Lazyfin change {Change} cannot apply to {Controller}.{Action}, because this Jellyfin server has no such API action",
                    change.Name,
                    action.Controller,
                    action.Action);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
