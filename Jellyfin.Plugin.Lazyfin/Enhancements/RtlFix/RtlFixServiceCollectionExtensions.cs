using System;
using System.Linq;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Lazyfin.Enhancements.RtlFix;

/// <summary>
/// Turns the RTL fix on in the Jellyfin server.
/// </summary>
public static class RtlFixServiceCollectionExtensions
{
    /// <summary>
    /// Wraps the server's <see cref="ISubtitleEncoder"/>, so every subtitle it hands to a client has its RTL lines laid
    /// out right to left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call it from a plugin's service registrator, after the server registered its own services. It replaces the last
    /// non-keyed <see cref="ISubtitleEncoder"/> registration with one that builds the RTL fix around it, with the same
    /// lifetime. When that registration names a type, the type is registered as itself, so the container still builds
    /// and disposes exactly one server encoder. When it is a factory or an instance, such as another plugin's decorator,
    /// that is wrapped too, so the other plugin stays in the chain.
    /// </para>
    /// <para>
    /// With no such registration, it leaves <paramref name="services"/> unchanged, and the server runs without the fix.
    /// Plugins register before any logger exists, so it cannot log this.
    /// </para>
    /// </remarks>
    /// <param name="services">The server's services.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddRtlFix(this IServiceCollection services)
    {
        var subtitleEncoder = FindCore<ISubtitleEncoder>(services);
        if (subtitleEncoder is null)
        {
            return services;
        }

        Decorate<ISubtitleEncoder>(services, subtitleEncoder, (sp, inner) => ActivatorUtilities.CreateInstance<RtlFixSubtitleEncoder>(sp, inner));
        return services;
    }

    private static ServiceDescriptor? FindCore<TService>(IServiceCollection services)
        => services.LastOrDefault(d => d.ServiceType == typeof(TService) && !d.IsKeyedService);

    private static void Decorate<TService>(IServiceCollection services, ServiceDescriptor core, Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        Func<IServiceProvider, TService> buildInner;
        if (core.ImplementationType is { } implementationType)
        {
            services.Add(new ServiceDescriptor(implementationType, implementationType, core.Lifetime));
            buildInner = sp => (TService)sp.GetRequiredService(implementationType);
        }
        else if (core.ImplementationFactory is { } factory)
        {
            buildInner = sp => (TService)factory(sp);
        }
        else
        {
            var instance = (TService)core.ImplementationInstance!;
            buildInner = _ => instance;
        }

        services.Remove(core);
        services.Add(new ServiceDescriptor(typeof(TService), sp => decorate(sp, buildInner(sp)), core.Lifetime));
    }
}
