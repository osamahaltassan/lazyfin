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
    /// Wraps the server's <see cref="ISubtitleEncoder"/> and <see cref="ITranscodeManager"/>, so every subtitle it hands
    /// to a client or burns into a video has its RTL lines laid out right to left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call it from a plugin's service registrator, after the server registered its own services. For each of the two
    /// services, it replaces the last non-keyed registration with one that builds the RTL fix around it, with the same
    /// lifetime. When that registration names a type, the type is registered as itself, so the container still builds
    /// and disposes exactly one server encoder and one server transcode manager. That matters most for the transcode
    /// manager, whose constructor empties the transcode folder. When it is a factory or an instance, such as another
    /// plugin's decorator, that is wrapped too, so the other plugin stays in the chain.
    /// </para>
    /// <para>
    /// When either registration is missing, it leaves <paramref name="services"/> unchanged, and the server runs without
    /// the fix. Plugins register before any logger exists, so it cannot log this.
    /// </para>
    /// </remarks>
    /// <param name="services">The server's services.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddRtlFix(this IServiceCollection services)
    {
        var subtitleEncoder = FindCore<ISubtitleEncoder>(services);
        var transcodeManager = FindCore<ITranscodeManager>(services);
        if (subtitleEncoder is null || transcodeManager is null)
        {
            return services;
        }

        Decorate<ISubtitleEncoder>(services, subtitleEncoder, (sp, inner) => ActivatorUtilities.CreateInstance<RtlFixSubtitleEncoder>(sp, inner));
        Decorate<ITranscodeManager>(services, transcodeManager, (sp, inner) => ActivatorUtilities.CreateInstance<RtlFixTranscodeManager>(sp, inner));
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
