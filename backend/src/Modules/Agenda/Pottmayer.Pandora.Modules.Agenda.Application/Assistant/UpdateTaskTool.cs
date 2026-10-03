using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>update_task</c>: renames a task, moves or clears its due date, or changes its priority
/// (<see cref="UpdateTaskCommand"/>). What the user did not mention keeps its current value.
/// </summary>
public sealed class UpdateTaskTool(ISender sender) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "update_task",
        Description: "Changes one of the user's tasks: its title, due date or priority. You cannot see the tasks: point at it by its number on the last list, or pass the words the user used to name it.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{TaskTarget.SchemaProperties}},
            "title": { "type": "string", "description": "The new title, only when the user renames it." },
            "due": { "type": "string", "description": "The new due date: a bare date (2026-09-05) when no time was said, otherwise an ISO-8601 timestamp with offset." },
            "clearDue": { "type": "boolean", "description": "True when the user wants no due date anymore." },
            "priority": { "type": "string", "enum": ["none", "low", "medium", "high"], "description": "The new priority, only when the user said it." }
          }
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample("push the passport task to next Monday", """{ "task": "passport", "due": "2026-09-14" }"""),
            new AssistantCommandExample("make 3 high priority", """{ "ref": 3, "priority": "high" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var changes = Changes(context, Parse(context, arguments));
        var name = TaskTarget.Name(arguments);
        return context.Text($"Alterar a tarefa \"{name}\"{changes}?", $"Change the task \"{name}\"{changes}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var change = Parse(context, arguments);
        if (change.Title is null && change.Due is null && !change.ClearDue && change.Priority is null)
            return AssistantCommandOutcome.Failed(context.Text("O que devo mudar na tarefa?", "What should I change on the task?"));

        var (task, problem) = await FindAsync(context, arguments, ct);
        if (task is null)
            return AssistantCommandOutcome.Failed(problem!);

        var (dueAt, dueHasTime) = change.ClearDue ? (null, false)
            : change.Due is { } due ? (due.At, due.HasTime)
            : (task.DueAt, task.DueHasTime);

        var result = await sender.Send(new UpdateTaskCommand(new UpdateTaskInput(
            context.UserId, task.Id, change.Title ?? task.Title, task.Notes, dueAt, dueHasTime,
            change.Priority ?? Enum.Parse<TaskPriority>(task.Priority))), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Tarefa \"{result.Value!.Title}\" alterada{Changes(context, change)}.",
            $"Task \"{result.Value!.Title}\" changed{Changes(context, change)}."));
    }

    private sealed record Change(string? Title, (DateTimeOffset At, bool HasTime)? Due, bool ClearDue, TaskPriority? Priority);

    private static Change Parse(AssistantToolContext context, JsonElement arguments) => new(
        OptionalString(arguments, "title"),
        OptionalDayOrInstant(context, arguments, "due"),
        OptionalBool(arguments, "clearDue"),
        OptionalString(arguments, "priority") is { } p ? Enum.Parse<TaskPriority>(p, ignoreCase: true) : null);

    private static string Changes(AssistantToolContext context, Change change)
    {
        var parts = new List<string>();
        if (change.Title is { } title)
            parts.Add(context.Text($"título \"{title}\"", $"title \"{title}\""));
        if (change.Due is { } due)
            parts.Add(context.Text("prazo ", "due ") + FormatDayOrInstant(context, due.At, due.HasTime));
        else if (change.ClearDue)
            parts.Add(context.Text("sem prazo", "no due date"));
        if (change.Priority is { } priority)
            parts.Add(context.Text("prioridade ", "priority ") + PriorityName(context, priority));
        return parts.Count == 0 ? "" : ": " + string.Join(", ", parts);
    }

    private static string PriorityName(AssistantToolContext context, TaskPriority priority) => priority switch
    {
        TaskPriority.Low => context.Text("baixa", "low"),
        TaskPriority.Medium => context.Text("média", "medium"),
        TaskPriority.High => context.Text("alta", "high"),
        _ => context.Text("nenhuma", "none"),
    };

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        TaskTarget.AsTarget(await FindAsync(context, arguments, ct));

    private Task<(TaskDto? Match, string? Problem)> FindAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct) =>
        TaskTarget.FindAsync(sender, context, arguments,
            t => t.Status != nameof(TaskItemStatus.Cancelled), "suas tarefas", "your tasks", ct);
}
