using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteEvent;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>delete_event</c>: removes an event (<see cref="DeleteEventCommand"/>) — of a repeating one, only the
/// occurrence meant unless the user said otherwise. Always confirmed first, whatever the user's level.
/// </summary>
public sealed class DeleteEventTool(ISender sender, TimeProvider timeProvider) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "delete_event",
        Description: "Deletes (cancels) one of the user's calendar events. You cannot see the events: point at it by its number on the last list, or pass the words the user used to name it.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{EventTarget.SchemaProperties}}
          }
        }
        """,
        Confirmation: ConfirmationPolicy.Required,
        Examples:
        [
            new AssistantCommandExample("cancel the dentist on Thursday", """{ "event": "dentist", "on": "2026-09-10" }"""),
            new AssistantCommandExample("delete 3", """{ "ref": 3 }"""),
            new AssistantCommandExample("delete the yoga class for good", """{ "event": "yoga", "scope": "all" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = EventTarget.Name(context, arguments);
        return ToolArguments.OptionalString(arguments, "scope") switch
        {
            "all" => context.Text($"Excluir o evento \"{name}\" e todas as ocorrências?", $"Delete the event \"{name}\" and every occurrence?"),
            "following" => context.Text($"Excluir o evento \"{name}\" e os seguintes?", $"Delete the event \"{name}\" and the following ones?"),
            _ => context.Text($"Excluir o evento \"{name}\"?", $"Delete the event \"{name}\"?"),
        };
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (target, problem) = await EventTarget.FindAsync(sender, context, arguments, timeProvider.GetUtcNow(), ct);
        if (target is null)
            return AssistantCommandOutcome.Failed(problem!);

        var result = await sender.Send(new DeleteEventCommand(new DeleteEventInput(
            context.UserId, target.Occurrence.EventId, target.Scope, target.OccurrenceStart)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Evento \"{target.Occurrence.Title}\"{EventTarget.Reach(context, target)} excluído.",
            $"Event \"{target.Occurrence.Title}\"{EventTarget.Reach(context, target)} deleted."));
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        EventTarget.AsTarget(await EventTarget.FindAsync(sender, context, arguments, timeProvider.GetUtcNow(), ct));
}
