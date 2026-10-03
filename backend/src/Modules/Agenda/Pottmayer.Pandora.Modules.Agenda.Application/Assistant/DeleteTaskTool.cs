using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>delete_task</c>: removes a task (<see cref="DeleteTaskCommand"/>). Always confirmed first, whatever
/// the user's level.
/// </summary>
public sealed class DeleteTaskTool(ISender sender) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "delete_task",
        Description: "Deletes one of the user's tasks. You cannot see the tasks: point at it by its number on the last list, or pass the words the user used to name it.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{TaskTarget.SchemaProperties}}
          }
        }
        """,
        Confirmation: ConfirmationPolicy.Required,
        Examples:
        [
            new AssistantCommandExample("delete the task about the printer ink", """{ "task": "printer ink" }"""),
            new AssistantCommandExample("delete 1", """{ "ref": 1 }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = TaskTarget.Name(arguments);
        return context.Text($"Excluir a tarefa \"{name}\"?", $"Delete the task \"{name}\"?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (task, problem) = await FindAsync(context, arguments, ct);
        if (task is null)
            return AssistantCommandOutcome.Failed(problem!);

        var result = await sender.Send(new DeleteTaskCommand(new DeleteTaskInput(context.UserId, task.Id)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text($"Tarefa \"{task.Title}\" excluída.", $"Task \"{task.Title}\" deleted."));
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        TaskTarget.AsTarget(await FindAsync(context, arguments, ct));

    private Task<(TaskDto? Match, string? Problem)> FindAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct) =>
        TaskTarget.FindAsync(sender, context, arguments,
            t => t.Status != nameof(TaskItemStatus.Cancelled), "suas tarefas", "your tasks", ct);
}
