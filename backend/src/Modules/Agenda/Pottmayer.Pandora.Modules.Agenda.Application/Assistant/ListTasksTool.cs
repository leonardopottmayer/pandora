using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskLists;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>list_tasks</c>: the user's tasks, optionally from one list, by due date (overdue, today, this week,
/// undated) or the finished ones. Due dates are bucketed in the user's zone here. Like <c>list_agenda</c>,
/// the numbered list goes to the user and the history keeps a content-free recap.
/// </summary>
public sealed class ListTasksTool(ISender sender, TimeProvider timeProvider) : IAssistantTool
{
    /// <summary>A chat reply stays readable; the Agenda shows the rest.</summary>
    private const int MaxShown = 40;

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "list_tasks",
        Description: "Shows the user their to-do tasks, optionally from one list or by due date. The numbered list goes to the user directly; you will not see it.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "list": { "type": "string", "description": "The task list as the user named it, when they asked for one." },
            "due": { "type": "string", "enum": ["overdue", "today", "week", "undated"], "description": "Only tasks overdue, due today, due in the next 7 days, or with no due date. Omit for all." },
            "done": { "type": "boolean", "description": "True to list finished tasks instead of open ones." }
          }
        }
        """,
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample("what are my tasks?", "{}"),
            new AssistantCommandExample("what's overdue?", """{ "due": "overdue" }"""),
            new AssistantCommandExample("show the work list for this week", """{ "list": "work", "due": "week" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments) =>
        context.Text("Mostrar suas tarefas?", "Show your tasks?");

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var due = OptionalString(arguments, "due");
        var done = OptionalBool(arguments, "done");

        var lists = (await sender.Send(new GetTaskListsQuery(new GetTaskListsInput(context.UserId)), ct)).Value ?? [];
        TaskListDto? list = null;
        if (OptionalString(arguments, "list") is { } listName)
        {
            (list, var problem) = PickByTitle(context, lists, l => l.Name, listName, "suas listas de tarefas", "your task lists");
            if (list is null)
                return AssistantCommandOutcome.Failed(problem!);
        }

        var result = await sender.Send(new GetTasksQuery(new GetTasksInput(context.UserId, list?.Id, null, null)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var now = timeProvider.GetUtcNow();
        var today = LocalDay(context, now);
        var tasks = (result.Value ?? [])
            .Where(t => done
                ? t.Status == nameof(TaskItemStatus.Done)
                : t.Status is nameof(TaskItemStatus.Todo) or nameof(TaskItemStatus.InProgress))
            .Where(t => due switch
            {
                "overdue" => IsOverdue(context, t, now, today),
                "today" => t.DueAt is { } d && LocalDay(context, d) == today,
                "week" => t.DueAt is { } d && LocalDay(context, d) is var day && day >= today && day < today.AddDays(7),
                "undated" => t.DueAt is null,
                _ => true,
            })
            .OrderBy(t => t.DueAt is null).ThenBy(t => t.DueAt).ThenBy(t => t.Position)
            .ToList();

        var recap = $"[list_tasks: {tasks.Count} task(s) shown to the user; content withheld from you]";
        if (tasks.Count == 0)
            return AssistantCommandOutcome.Ok(context.Text("Nenhuma tarefa encontrada.", "No tasks found."), recap);

        // The list's name helps only when the tasks come from more than one.
        var listNames = list is null && tasks.Select(t => t.ListId).Distinct().Count() > 1
            ? lists.ToDictionary(l => l.Id, l => l.Name)
            : null;

        var sb = new StringBuilder(list is null
            ? context.Text("Suas tarefas:", "Your tasks:")
            : context.Text($"Tarefas de {list.Name}:", $"Tasks in {list.Name}:"));
        var listed = new List<ListedItem>();
        foreach (var task in tasks.Take(MaxShown))
        {
            listed.Add(new ListedItem("task", task.Id, task.Title));
            sb.Append('\n').Append(listed.Count).Append(". ").Append(task.Title);
            if (task.DueAt is { } dueAt)
                sb.Append(" · ").Append(FormatDayOrInstant(context, dueAt, task.DueHasTime));
            if (!done && IsOverdue(context, task, now, today))
                sb.Append(context.Text(" (atrasada)", " (overdue)"));
            if (listNames?.GetValueOrDefault(task.ListId) is { } name)
                sb.Append(" — ").Append(name);
        }
        if (tasks.Count > MaxShown)
            sb.Append('\n').Append(context.Text(
                $"…e mais {tasks.Count - MaxShown}. A lista completa está na Agenda.",
                $"…and {tasks.Count - MaxShown} more. The full list is in the Agenda."));

        return AssistantCommandOutcome.Ok(sb.ToString(), recap, listed);
    }

    private static bool IsOverdue(AssistantToolContext context, TaskDto task, DateTimeOffset now, DateOnly today) =>
        task.DueAt is { } due && (task.DueHasTime ? due < now : LocalDay(context, due) < today);

    private static DateOnly LocalDay(AssistantToolContext context, DateTimeOffset at) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, context.TimeZone).DateTime);
}
