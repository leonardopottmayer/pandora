using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>complete_task</c>: marks an open task done. The model passes only the words the user said, or the
/// task's number on a list; the task is found here among the user's open tasks (<see cref="TaskTarget"/>),
/// so no task title is ever sent to the model. Forwarded to <see cref="CompleteTaskCommand"/>.
/// </summary>
public sealed class CompleteTaskTool(ISender sender) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "complete_task",
        Description: "Marks one of the user's open tasks as done. You cannot see the tasks: point at it by its number on the last list, or pass the words the user used to name it; the system finds it.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{TaskTarget.SchemaProperties}}
          }
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample("mark renew passport as done", """{ "task": "renew passport" }"""),
            new AssistantCommandExample("I already bought the printer ink", """{ "task": "printer ink" }"""),
            new AssistantCommandExample("done with 2", """{ "ref": 2 }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = TaskTarget.Name(arguments);
        return context.Text($"Concluir a tarefa \"{name}\"?", $"Mark the task \"{name}\" as done?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (task, problem) = await FindAsync(context, arguments, ct);
        if (task is null)
            return AssistantCommandOutcome.Failed(problem!);

        var result = await sender.Send(new CompleteTaskCommand(new CompleteTaskInput(context.UserId, task.Id)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Tarefa \"{task.Title}\" concluída.",
            $"Task \"{task.Title}\" marked as done."));
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        TaskTarget.AsTarget(await FindAsync(context, arguments, ct));

    private Task<(TaskDto? Match, string? Problem)> FindAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct) =>
        TaskTarget.FindAsync(sender, context, arguments,
            t => t.Status is nameof(TaskItemStatus.Todo) or nameof(TaskItemStatus.InProgress),
            "suas tarefas abertas", "your open tasks", ct);
}
