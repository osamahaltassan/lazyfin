using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Lazyfin.ServerRoute;

/// <summary>
/// One Jellyfin API action that an <see cref="ApiChange"/> changes, and what the change needs from it.
/// </summary>
/// <remarks>
/// <para>
/// List every action whose request or answer should change, legacy actions included. A legacy action such as
/// <c>GetResumeItemsLegacy</c> calls the current method directly, so the filter sees only the legacy action.
/// </para>
/// <para>
/// The server-build test in <c>tests/</c> checks each listed action, its answer type, and its arguments against every
/// Jellyfin build in <c>SERVERS</c> in <c>scripts/dev.sh</c>, so a rename fails CI instead of stopping the change
/// silently.
/// </para>
/// </remarks>
/// <param name="Controller">The controller name as MVC sees it, the class name without <c>Controller</c>, such as <c>TvShows</c>.</param>
/// <param name="Action">The action name, which is the method name in <c>Jellyfin.Api</c>, such as <c>GetNextUp</c>.</param>
/// <param name="Answer">The value type the action answers with, after <c>Task&lt;&gt;</c> and <c>ActionResult&lt;&gt;</c> are taken off.</param>
/// <param name="Arguments">Every action argument the change reads or writes. The change may write only these.</param>
public sealed record ApiAction(string Controller, string Action, Type Answer, IReadOnlyList<ApiArgument> Arguments)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiAction"/> class for a change that reads and writes no arguments.
    /// </summary>
    /// <param name="controller">The controller name.</param>
    /// <param name="action">The action name.</param>
    /// <param name="answer">The answer type.</param>
    public ApiAction(string controller, string action, Type answer)
        : this(controller, action, answer, [])
    {
    }
}
