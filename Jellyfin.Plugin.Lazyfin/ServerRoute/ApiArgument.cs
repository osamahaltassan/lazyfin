using System;
using System.Linq;

namespace Jellyfin.Plugin.Lazyfin.ServerRoute;

/// <summary>
/// One action argument an <see cref="ApiChange"/> reads or writes, written as type names because its type may live in
/// <c>Jellyfin.Api</c>, which Lazyfin cannot reference.
/// </summary>
/// <remarks>
/// Use <see cref="Of{T}"/> when the type is one Lazyfin can reference. Write the name by hand only for a
/// <c>Jellyfin.Api</c> type, such as <c>Jellyfin.Api.Models.MediaInfoDtos.PlaybackInfoDto</c>, and then name the
/// property the change reads by name too, so the server-build test checks it.
/// </remarks>
/// <param name="Name">The parameter name, which is the key in the action's arguments.</param>
/// <param name="Type">The parameter type's full name, generic arguments in brackets, such as <c>System.Nullable`1[System.Int32]</c>.</param>
/// <param name="Property">A property of that type the change reads by name, or <c>null</c>.</param>
/// <param name="PropertyType">That property's type's full name, or <c>null</c>.</param>
public sealed record ApiArgument(string Name, string Type, string? Property = null, string? PropertyType = null)
{
    /// <summary>
    /// Describes an argument of a type Lazyfin can reference.
    /// </summary>
    /// <typeparam name="T">The parameter type, such as <c>int?</c>.</typeparam>
    /// <param name="name">The parameter name.</param>
    /// <returns>The argument.</returns>
    public static ApiArgument Of<T>(string name) => new(name, TypeName(typeof(T)));

    /// <summary>
    /// Writes <paramref name="type"/> the way <see cref="Type"/> names it here: its full name, with generic arguments in
    /// brackets, such as <c>System.Nullable`1[System.DateTime]</c>.
    /// </summary>
    /// <param name="type">A type.</param>
    /// <returns>Its name.</returns>
    public static string TypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.IsGenericType
            ? $"{type.GetGenericTypeDefinition().FullName}[{string.Join(",", type.GetGenericArguments().Select(TypeName))}]"
            : type.FullName!;
    }
}
