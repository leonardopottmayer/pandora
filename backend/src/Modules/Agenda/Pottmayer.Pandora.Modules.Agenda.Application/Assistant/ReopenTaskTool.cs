using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.ReopenTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary><c>reopen_task</c>: puts a finished task back to do (<see cref="ReopenTaskCommand"/>).</summary>
public sealed class ReopenTaskTool(ISender sender) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "reopen_task",
        Description: "Reopens one of the user's finished tasks (marks it as not done). You cannot see the tasks: point at it by its number on the last list, or pass the words the user used to name it.",
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
            new AssistantCommandExample("I didn't actually finish the report, reopen it", """{ "task": "report" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = TaskTarget.Name(arguments);
        return context.Text($"Reabrir a tarefa \"{name}\"?", $"Reopen the task \"{name}\"?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (task, problem) = await FindAsync(context, arguments, ct);
        if (task is null)
            return AssistantCommandOutcome.Failed(problem!);

        var result = await sender.Send(new ReopenTaskCommand(new ReopenTaskInput(context.UserId, task.Id)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text($"Tarefa \"{task.Title}\" reaberta.", $"Task \"{task.Title}\" reopened."));
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        TaskTarget.AsTarget(await FindAsync(context, arguments, ct));

    private Task<(TaskDto? Match, string? Problem)> FindAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct) =>
        TaskTarget.FindAsync(sender, context, arguments,
            t => t.Status == nameof(TaskItemStatus.Done), "suas tarefas concluídas", "your finished tasks", ct);
}
