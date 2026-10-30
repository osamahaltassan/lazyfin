using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Jellyfin.Plugin.Lazyfin.Tests.ServerRoute;

/// <summary>
/// The controller actions and property types one server build's <c>Jellyfin.Api.dll</c> declares, read from its
/// metadata without loading it.
/// </summary>
/// <remarks>
/// Type names are full names with generic arguments in brackets, such as
/// <c>System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Mvc.ActionResult`1[MediaBrowser.Model.Dto.BaseItemDto]]</c>,
/// the way <see cref="Jellyfin.Plugin.Lazyfin.ServerRoute.ApiArgument.TypeName"/> writes a loaded type.
/// </remarks>
internal sealed class JellyfinApiMetadata
{
    private JellyfinApiMetadata(
        IReadOnlyDictionary<(string Controller, string Action), IReadOnlyList<ApiMethod>> actions,
        IReadOnlyDictionary<(string Type, string Property), string> properties)
    {
        Actions = actions;
        Properties = properties;
    }

    /// <summary>
    /// Gets the public instance methods of every controller in <c>Jellyfin.Api.Controllers</c>, by controller name
    /// without <c>Controller</c> and method name. Overloads share one key.
    /// </summary>
    public IReadOnlyDictionary<(string Controller, string Action), IReadOnlyList<ApiMethod>> Actions { get; }

    /// <summary>
    /// Gets the type of every property declared in the assembly, by declaring type and property name.
    /// </summary>
    public IReadOnlyDictionary<(string Type, string Property), string> Properties { get; }

    /// <summary>
    /// Reads <paramref name="path"/>.
    /// </summary>
    /// <param name="path">A <c>Jellyfin.Api.dll</c>.</param>
    /// <returns>Its actions and properties.</returns>
    public static JellyfinApiMetadata Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var names = new TypeNames();
        var actions = new Dictionary<(string, string), List<ApiMethod>>();
        var properties = new Dictionary<(string, string), string>();
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            var typeName = names.GetTypeFromDefinition(reader, typeHandle, 0);
            foreach (var propertyHandle in type.GetProperties())
            {
                var property = reader.GetPropertyDefinition(propertyHandle);
                properties[(typeName, reader.GetString(property.Name))] = property.DecodeSignature(names, null).ReturnType;
            }

            var ns = reader.GetString(type.Namespace);
            var name = reader.GetString(type.Name);
            if (ns != "Jellyfin.Api.Controllers"
                || !name.EndsWith("Controller", StringComparison.Ordinal)
                || (type.Attributes & TypeAttributes.Abstract) != 0)
            {
                continue;
            }

            var controller = name[..^"Controller".Length];
            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                var attributes = method.Attributes;
                if ((attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public
                    || (attributes & (MethodAttributes.Static | MethodAttributes.SpecialName)) != 0)
                {
                    continue;
                }

                var signature = method.DecodeSignature(names, null);
                var parameters = new Dictionary<string, string>();
                foreach (var parameterHandle in method.GetParameters())
                {
                    var parameter = reader.GetParameter(parameterHandle);
                    if (parameter.SequenceNumber > 0)
                    {
                        parameters[reader.GetString(parameter.Name)] = signature.ParameterTypes[parameter.SequenceNumber - 1];
                    }
                }

                var key = (controller, reader.GetString(method.Name));
                if (!actions.TryGetValue(key, out var overloads))
                {
                    actions[key] = overloads = [];
                }

                overloads.Add(new ApiMethod(signature.ReturnType, parameters));
            }
        }

        return new JellyfinApiMetadata(
            actions.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ApiMethod>)pair.Value),
            properties);
    }

    /// <summary>
    /// Takes <c>Task&lt;&gt;</c> and <c>ActionResult&lt;&gt;</c> off an action's return type.
    /// </summary>
    /// <param name="returnType">The return type name.</param>
    /// <returns>The value type the action answers with.</returns>
    public static string Unwrap(string returnType)
    {
        foreach (var wrapper in new[] { "System.Threading.Tasks.Task`1[", "Microsoft.AspNetCore.Mvc.ActionResult`1[" })
        {
            if (returnType.StartsWith(wrapper, StringComparison.Ordinal) && returnType.EndsWith(']'))
            {
                returnType = returnType[wrapper.Length..^1];
            }
        }

        return returnType;
    }

    /// <summary>
    /// One controller method.
    /// </summary>
    /// <param name="ReturnType">Its return type name.</param>
    /// <param name="Parameters">Its parameter type names, by parameter name.</param>
    internal sealed record ApiMethod(string ReturnType, IReadOnlyDictionary<string, string> Parameters);

    private sealed class TypeNames : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + typeCode;

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = reader.GetString(type.Name);
            var declaring = type.GetDeclaringType();
            return declaring.IsNil
                ? $"{reader.GetString(type.Namespace)}.{name}"
                : $"{GetTypeFromDefinition(reader, declaring, rawTypeKind)}+{name}";
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeReference(handle);
            var name = reader.GetString(type.Name);
            return type.ResolutionScope.Kind == HandleKind.TypeReference
                ? $"{GetTypeFromReference(reader, (TypeReferenceHandle)type.ResolutionScope, rawTypeKind)}+{name}"
                : $"{reader.GetString(type.Namespace)}.{name}";
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            => $"{genericType}[{string.Join(",", typeArguments)}]";

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";

        public string GetByReferenceType(string elementType) => elementType + "&";

        public string GetPointerType(string elementType) => elementType + "*";

        public string GetPinnedType(string elementType) => elementType;

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;

        public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;

        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    }
}
