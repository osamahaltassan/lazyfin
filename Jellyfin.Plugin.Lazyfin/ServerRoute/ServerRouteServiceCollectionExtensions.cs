using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Lazyfin.ServerRoute;

/// <summary>
/// Plugs an enhancement's API changes into the server route.
/// </summary>
public static class ServerRouteServiceCollectionExtensions
{
    /// <summary>
    /// Registers <paramref name="change"/>, and on the first call adds <see cref="ServerRouteFilter"/> to every MVC
    /// action and <see cref="MissingApiActionCheck"/> to server start.
    /// </summary>
    /// <remarks>
    /// Call it from an enhancement's <c>Add&lt;Enhancement&gt;()</c>. Changes on one action run in the order they were
    /// added. Lazyfin promises no order against other plugins' MVC filters.
    /// </remarks>
    /// <param name="services">The server's services.</param>
    /// <param name="change">The change.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddApiChange(this IServiceCollection services, ApiChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!services.Any(d => d.ServiceType == typeof(ServerRouteFilter)))
        {
            services.AddSingleton<ServerRouteFilter>();
            services.Configure<MvcOptions>(options => options.Filters.AddService<ServerRouteFilter>());
            services.AddHostedService<MissingApiActionCheck>();
        }

        services.AddSingleton(change);
        return services;
    }
}
