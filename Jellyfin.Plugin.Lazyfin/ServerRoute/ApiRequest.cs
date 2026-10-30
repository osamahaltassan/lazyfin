using System;

namespace Jellyfin.Plugin.Lazyfin.ServerRoute;

/// <summary>
/// What an <see cref="ApiChange"/> may know about the request it changes.
/// </summary>
/// <remarks>
/// <para>
/// Anything else about the viewer, such as their settings, comes from Jellyfin's services, looked up by
/// <see cref="UserId"/>.
/// </para>
/// <para>
/// Any client can send any <see cref="Client"/> name. Use it only to aim a change at the clients it helps, by exact
/// name, never to decide access. A client the change does not recognize gets Jellyfin's own answer.
/// </para>
/// </remarks>
/// <param name="UserId">The signed-in viewer, or <see cref="Guid.Empty"/> on a request with no token.</param>
/// <param name="Client">The exact <c>Client</c> name from the <c>Authorization</c> header, such as <c>Jellyfin Web</c>, or <c>null</c>.</param>
/// <param name="DeviceId">The device ID from the <c>Authorization</c> header, or <c>null</c>.</param>
/// <param name="UserAgent">The <c>User-Agent</c> header, or <c>null</c>. Use it only where the client name cannot tell two clients apart.</param>
public sealed record ApiRequest(Guid UserId, string? Client, string? DeviceId, string? UserAgent);
