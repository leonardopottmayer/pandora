using System.Text.Json;

namespace Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;

/// <summary>
/// A tool whose call acts on one existing item (a task, an event, a reminder, a note). Before such a call is
/// held for confirmation, the pipeline asks the tool to find that item: when there is none (or several), the
/// user hears it at once instead of after tapping Confirm; when there is one, it is pinned into the arguments
/// like a listed item (<see cref="ListedRefs.PinTarget"/>), so the question names it as it really is and the
/// confirmed call acts on exactly it.
/// </summary>
public interface IAssistantTargetedTool : IAssistantTool
{
    /// <summary>The item the call names, or why there is not exactly one — the same lookup the call runs.</summary>
    Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default);
}
