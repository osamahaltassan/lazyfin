using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Jellyfin.Plugin.Lazyfin.ServerRoute;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.Lazyfin.Tests.ServerRoute;

/// <summary>
/// Runs <see cref="ServerRouteFilter"/> around one fake MVC action, the way MVC runs it around a Jellyfin action.
/// </summary>
/// <remarks>
/// Set the action's parameters, the arguments a client sent, the request's claims, and the answer the action returns,
/// then call <see cref="RunAsync"/>. Afterwards <see cref="Arguments"/> holds what the action ran with,
/// <see cref="ArgumentsSeen"/> what it saw, and the returned value is what the client gets.
/// </remarks>
internal sealed class ActionRun
{
    public ActionRun(string controller, string action, params (string Name, Type Type)[] parameters)
    {
        Descriptor = new ControllerActionDescriptor
        {
            ControllerName = controller,
            ActionName = action,
            Parameters = parameters.Select(p => new ParameterDescriptor { Name = p.Name, ParameterType = p.Type }).ToList(),
        };
    }

    public ControllerActionDescriptor Descriptor { get; }

    public Dictionary<string, object?> Arguments { get; } = [];

    public IReadOnlyDictionary<string, object?>? ArgumentsSeen { get; private set; }

    public List<Claim> Claims { get; } = [];

    public string? UserAgent { get; set; }

    public IActionResult? Result { get; set; }

    public Exception? Exception { get; set; }

    public async Task<object?> RunAsync(params ApiChange[] changes)
    {
        var filter = new ServerRouteFilter(changes, NullLogger<ServerRouteFilter>.Instance);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(Claims, "Test")) };
        if (UserAgent is not null)
        {
            httpContext.Request.Headers.UserAgent = UserAgent;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), Descriptor);
        var filters = new List<IFilterMetadata>();
        var executing = new ActionExecutingContext(actionContext, filters, Arguments, new object());
        ActionExecutedContext? executed = null;

        await filter.OnActionExecutionAsync(executing, () =>
        {
            ArgumentsSeen = new Dictionary<string, object?>(Arguments);
            executed = new ActionExecutedContext(actionContext, filters, new object()) { Result = Result, Exception = Exception };
            return Task.FromResult(executed);
        });

        return executed?.Result is ObjectResult objectResult ? objectResult.Value : null;
    }
}
