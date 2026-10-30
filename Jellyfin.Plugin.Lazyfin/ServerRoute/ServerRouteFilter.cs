using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Lazyfin.ServerRoute;

/// <summary>
/// The one global MVC action filter that applies every registered <see cref="ApiChange"/> to the Jellyfin API.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ServerRouteServiceCollectionExtensions.AddApiChange"/> adds it to every MVC action once. It matches the
/// running action by controller and action name. Before the action runs, it hands each change a copy of the bound
/// arguments and writes back the ones the change returns, only once the change finished without throwing and every
/// value fits its parameter. After the action runs, it hands each change the answer of a successful
/// <see cref="ObjectResult"/>.
/// </para>
/// <para>
/// It fails open: a change that throws, writes an argument it does not list or of the wrong type, or meets an answer of
/// a type it does not list is skipped, and Jellyfin's own request or answer goes on. Each kind of failure is logged once
/// per change and action per server run, never once per request.
/// </para>
/// </remarks>
public sealed class ServerRouteFilter : IAsyncActionFilter
{
    private const string UserIdClaim = "Jellyfin-UserId";
    private const string ClientClaim = "Jellyfin-Client";
    private const string DeviceIdClaim = "Jellyfin-DeviceId";

    private readonly Dictionary<(string Controller, string Action), (ApiChange Change, ApiAction Action)[]> _changes;
    private readonly ILogger<ServerRouteFilter> _logger;
    private readonly ConcurrentDictionary<string, byte> _logged = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerRouteFilter"/> class.
    /// </summary>
    /// <param name="changes">Every registered change, in registration order.</param>
    /// <param name="logger">The logger.</param>
    public ServerRouteFilter(IEnumerable<ApiChange> changes, ILogger<ServerRouteFilter> logger)
    {
        _changes = changes
            .SelectMany(change => change.Actions.Select(action => (Change: change, Action: action)))
            .GroupBy(pair => (pair.Action.Controller, pair.Action.Action))
            .ToDictionary(group => group.Key, group => group.ToArray());
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor
            || !_changes.TryGetValue((descriptor.ControllerName, descriptor.ActionName), out var changes))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var request = ReadRequest(context.HttpContext);
        foreach (var (change, action) in changes)
        {
            if (change.ChangeArguments is not null)
            {
                ChangeArguments(context, descriptor, change, action, request);
            }
        }

        var executed = await next().ConfigureAwait(false);
        if (executed.Exception is not null
            || executed.Result is not ObjectResult { Value: { } answer } result
            || result.StatusCode is < 200 or >= 300)
        {
            return;
        }

        foreach (var (change, action) in changes)
        {
            if (change.ChangeAnswer is not null)
            {
                answer = ChangeAnswer(change, action, answer, request);
            }
        }

        result.Value = answer;
    }

    private static ApiRequest ReadRequest(HttpContext httpContext)
    {
        var user = httpContext.User;
        var userId = Guid.TryParse(user.FindFirst(UserIdClaim)?.Value, out var id) ? id : Guid.Empty;
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return new ApiRequest(
            userId,
            user.FindFirst(ClientClaim)?.Value,
            user.FindFirst(DeviceIdClaim)?.Value,
            userAgent.Length == 0 ? null : userAgent);
    }

    private static bool Fits(Type parameterType, object? value)
        => value is null
            ? !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) is not null
            : parameterType.IsInstanceOfType(value);

    private void ChangeArguments(ActionExecutingContext context, ControllerActionDescriptor descriptor, ApiChange change, ApiAction action, ApiRequest request)
    {
        IReadOnlyDictionary<string, object?> written;
        try
        {
            written = change.ChangeArguments!(new Dictionary<string, object?>(context.ActionArguments), request);
        }
        catch (Exception ex)
        {
            LogOnce(change, action, "threw while changing arguments", ex);
            return;
        }

        foreach (var (name, value) in written)
        {
            var parameter = descriptor.Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            if (!action.Arguments.Any(a => string.Equals(a.Name, name, StringComparison.Ordinal))
                || parameter is null
                || !Fits(parameter.ParameterType, value))
            {
                LogOnce(change, action, $"wrote argument {name}, which it does not list or which does not fit", null);
                return;
            }
        }

        foreach (var (name, value) in written)
        {
            context.ActionArguments[name] = value;
        }
    }

    private object ChangeAnswer(ApiChange change, ApiAction action, object answer, ApiRequest request)
    {
        if (!action.Answer.IsInstanceOfType(answer))
        {
            LogOnce(change, action, $"met an answer of type {answer.GetType()}, not {action.Answer}", null);
            return answer;
        }

        try
        {
            var changed = change.ChangeAnswer!(answer, request);
            if (action.Answer.IsInstanceOfType(changed))
            {
                return changed;
            }

            LogOnce(change, action, $"returned an answer of type {changed?.GetType().ToString() ?? "null"}, not {action.Answer}", null);
        }
        catch (Exception ex)
        {
            LogOnce(change, action, "threw while changing the answer", ex);
        }

        return answer;
    }

    private void LogOnce(ApiChange change, ApiAction action, string problem, Exception? exception)
    {
        if (!_logged.TryAdd($"{change.Name}|{action.Controller}.{action.Action}|{problem}", 0))
        {
            return;
        }

        _logger.LogWarning(
            exception,
            "Lazyfin change {Change} {Problem} on {Controller}.{Action}, so Jellyfin's own value passes through. Lazyfin logs this once per server run",
            change.Name,
            problem,
            action.Controller,
            action.Action);
    }
}
