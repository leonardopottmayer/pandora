using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskLists;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>create_task</c>: a to-do in the list the user named or else their default one (the first open one when
/// none is marked default), forwarded to <see cref="CreateTaskCommand"/>. The due date may be a bare day or a
/// time; a task may recur (it then needs a due date) or sit under another task as a subtask.
/// </summary>
public sealed class CreateTaskTool(ISender sender) : IAssistantTool
{
    public IReadOnlyList<string> Permissions { get; } =
        [AgendaPermissions.Tasks.Create, AgendaPermissions.Lists.Read, AgendaPermissions.Tasks.Read];

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "create_task",
        Description: "Creates a to-do task, optionally with a due date. Use when the user wants to note something to do (not to be pinged at a time — that is create_reminder).",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "What to do, in a few words." },
            "due": { "type": "string", "description": "When it is due: a bare date (2026-09-05) when no time was said, otherwise an ISO-8601 timestamp with offset." },
            "priority": { "type": "string", "enum": ["low", "medium", "high"], "description": "Only when the user said how important it is." },
            "notes": { "type": "string", "description": "Details about the task, only when the user gave them." },
            "list": { "type": "string", "description": "The task list's name as the user said it (\"Compras\", \"Work\"), only when they named one." },
            "parent": { "type": "string", "description": "Only for a subtask: the existing task it goes under, as the user named it." },
            {{AgendaToolArguments.RecurrenceSchema}}
          },
          "required": ["title"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "add a task to renew my passport by Friday",
                """{ "title": "Renew passport", "due": "2026-09-11" }"""),
            new AssistantCommandExample(
                "to-do: buy printer ink, urgent",
                """{ "title": "Buy printer ink", "priority": "high" }"""),
            new AssistantCommandExample(
                "add milk to the groceries list",
                """{ "title": "Milk", "list": "groceries" }"""),
            new AssistantCommandExample(
                "pay the electricity bill every month on the 10th",
                """{ "title": "Pay the electricity bill", "due": "2026-09-10", "recurrence": "FREQ=MONTHLY;BYMONTHDAY=10" }"""),
            new AssistantCommandExample(
                "under the trip task, add book the hotel",
                """{ "title": "Book the hotel", "parent": "trip" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var t = Parse(context, arguments);
        var repeats = AgendaToolArguments.Repeats(context, t.Rrule);
        return context.Text(
            $"Criar a tarefa \"{t.Title}\"{DuePt(context, t.Due)}{repeats}?",
            $"Create the task \"{t.Title}\"{DueEn(context, t.Due)}{repeats}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var t = Parse(context, arguments);

        var lists = await sender.Send(new GetTaskListsQuery(new GetTaskListsInput(context.UserId)), ct);
        var open = (lists.Value ?? []).Where(l => l.ArchivedAt is null).ToList();

        // A subtask lives in its parent's list.
        TaskDto? parent = null;
        if (OptionalString(arguments, "parent") is { } parentName)
        {
            var tasks = await sender.Send(new GetTasksQuery(new GetTasksInput(context.UserId, null, null, null)), ct);
            var candidates = (tasks.Value ?? []).Where(x => x.ParentTaskId is null
                && x.Status is not (nameof(TaskItemStatus.Done) or nameof(TaskItemStatus.Cancelled)));
            var (match, parentProblem) = PickByTitle(context, candidates, x => x.Title, parentName, "suas tarefas", "your tasks");
            if (match is null)
                return AssistantCommandOutcome.Failed(parentProblem!);
            parent = match;
        }

        var (list, problem) = parent is not null
            ? (open.FirstOrDefault(l => l.Id == parent.ListId), null)
            : AgendaToolArguments.PickNamedOrDefault(
                context, arguments, "list", open, l => l.Name, l => l.IsDefault, "suas listas", "your lists");
        if (list is null)
            return AssistantCommandOutcome.Failed(problem ?? context.Text(
                "Você ainda não tem nenhuma lista de tarefas. Crie uma na Agenda primeiro.",
                "You don't have a task list yet. Create one in the Agenda first."));

        var result = await sender.Send(new CreateTaskCommand(new CreateTaskInput(
            context.UserId, list.Id, parent?.Id, t.Title, t.Notes, t.Due?.At, t.Due?.HasTime ?? false,
            t.Priority, TimeZone: null, t.Rrule)), ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var where = parent is not null
            ? context.Text($"dentro de \"{parent.Title}\"", $"under \"{parent.Title}\"")
            : context.Text($"em {list.Name}", $"in {list.Name}");
        var repeats = AgendaToolArguments.Repeats(context, t.Rrule);
        return AssistantCommandOutcome.Ok(context.Text(
            $"Tarefa \"{result.Value!.Title}\" criada {where}{DuePt(context, t.Due)}{repeats}.",
            $"Task \"{result.Value!.Title}\" created {where}{DueEn(context, t.Due)}{repeats}."));
    }

    private static string DuePt(AssistantToolContext context, (DateTimeOffset At, bool HasTime)? due) =>
        due is { } d ? $" para {FormatDayOrInstant(context, d.At, d.HasTime)}" : "";

    private static string DueEn(AssistantToolContext context, (DateTimeOffset At, bool HasTime)? due) =>
        due is { } d ? $" due {FormatDayOrInstant(context, d.At, d.HasTime)}" : "";

    private sealed record Args(
        string Title, (DateTimeOffset At, bool HasTime)? Due, TaskPriority Priority, string? Notes, string? Rrule);

    private static Args Parse(AssistantToolContext context, JsonElement arguments)
    {
        var priority = OptionalString(arguments, "priority") is { } p
            ? Enum.TryParse<TaskPriority>(p, ignoreCase: true, out var parsed) ? parsed : TaskPriority.None
            : TaskPriority.None;
        return new Args(RequiredString(arguments, "title"), OptionalDayOrInstant(context, arguments, "due"), priority,
            OptionalString(arguments, "notes"), AgendaToolArguments.Recurrence(arguments));
    }
}
