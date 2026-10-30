using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Lazyfin.ServerRoute;

/// <summary>
/// Changes the bound arguments of an action before it runs.
/// </summary>
/// <param name="arguments">The action's arguments, by parameter name. An argument the client did not send is missing.</param>
/// <param name="request">Who sent the request.</param>
/// <returns>
/// Only the arguments to change, by parameter name. A <c>null</c> value means "as if the client sent nothing". Each name
/// must be listed in <see cref="ApiAction.Arguments"/>.
/// </returns>
public delegate IReadOnlyDictionary<string, object?> ArgumentsChange(IReadOnlyDictionary<string, object?> arguments, ApiRequest request);

/// <summary>
/// Changes the answer of an action after it ran.
/// </summary>
/// <param name="answer">The answer, of the <see cref="ApiAction.Answer"/> type.</param>
/// <param name="request">Who sent the request.</param>
/// <returns>The answer to send, either <paramref name="answer"/> or a new value of the same type.</returns>
public delegate object AnswerChange(object answer, ApiRequest request);

/// <summary>
/// One change an enhancement makes to Jellyfin API requests or answers, through the server route.
/// </summary>
/// <remarks>
/// <para>
/// Register it from the enhancement's <c>Add&lt;Enhancement&gt;()</c> with
/// <see cref="ServerRouteServiceCollectionExtensions.AddApiChange"/>. Keep the change logic in plain methods over
/// <c>Jellyfin.Model</c> types in the enhancement's own folder, and use this type only to connect that logic to actions.
/// </para>
/// <para>
/// Several changes on one action run in registration order, and each sees the previous one's output.
/// </para>
/// <para>
/// A change fails open. When it throws, writes an argument it does not list or of the wrong type, or meets an answer of
/// a type it does not list, the filter keeps Jellyfin's own request or answer and logs it once per server run.
/// </para>
/// <para>
/// Edit an answer in place only on an action that Jellyfin's source shows builds a fresh answer per request on every
/// supported server version. Otherwise return a new value. An action such as <c>GetConfiguration</c> answers with the
/// server's live object, and editing it would change the server's real settings.
/// </para>
/// </remarks>
/// <param name="Name">A short name for logs, such as <c>NextUpNoLimit</c>.</param>
/// <param name="Actions">The actions this change applies to.</param>
public sealed record ApiChange(string Name, IReadOnlyList<ApiAction> Actions)
{
    /// <summary>
    /// Gets the change to the action's arguments, or <c>null</c> when it changes none.
    /// </summary>
    public ArgumentsChange? ChangeArguments { get; init; }

    /// <summary>
    /// Gets the change to the action's answer, or <c>null</c> when it changes none.
    /// </summary>
    public AnswerChange? ChangeAnswer { get; init; }

    /// <summary>
    /// Wraps a typed answer change for <see cref="ChangeAnswer"/>.
    /// </summary>
    /// <typeparam name="T">The answer type, which must be the <see cref="ApiAction.Answer"/> of every listed action.</typeparam>
    /// <param name="change">The change.</param>
    /// <returns>The untyped change.</returns>
    public static AnswerChange Answer<T>(Func<T, ApiRequest, T> change)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(change);
        return (answer, request) => change((T)answer, request);
    }
}
