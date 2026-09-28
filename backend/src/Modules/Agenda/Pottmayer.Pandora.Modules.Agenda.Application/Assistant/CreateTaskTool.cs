using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskLists;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>create_task</c>: a to-do in the user's default task list (the first open one when none is marked
/// default), forwarded to <see cref="CreateTaskCommand"/>. The due date may be a bare day or a time.
/// </summary>
public sealed class CreateTaskTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "create_task",
        Description: "Creates a to-do task, optionally with a due date. Use when the user wants to note something to do (not to be pinged at a time — that is create_reminder).",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "What to do, in a few words." },
            "due": { "type": "string", "description": "When it is due: a bare date (2026-09-05) when no time was said, otherwise an ISO-8601 timestamp with offset." },
            "priority": { "type": "string", "enum": ["low", "medium", "high"], "description": "Only when the user said how important it is." }
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
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var (title, due, _) = Parse(context, arguments);
        return context.Text(
            $"Criar a tarefa \"{title}\"{DuePt(context, due)}?",
            $"Create the task \"{title}\"{DueEn(context, due)}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (title, due, priority) = Parse(context, arguments);

        var lists = await sender.Send(new GetTaskListsQuery(new GetTaskListsInput(context.UserId)), ct);
        var open = (lists.Value ?? []).Where(l => l.ArchivedAt is null).ToList();
        var list = open.FirstOrDefault(l => l.IsDefault) ?? open.FirstOrDefault();
        if (list is null)
            return AssistantCommandOutcome.Failed(context.Text(
                "Você ainda não tem nenhuma lista de tarefas. Crie uma na Agenda primeiro.",
                "You don't have a task list yet. Create one in the Agenda first."));

        var result = await sender.Send(new CreateTaskCommand(new CreateTaskInput(
            context.UserId, list.Id, ParentTaskId: null, title, Notes: null, due?.At, due?.HasTime ?? false,
            priority, TimeZone: null, Rrule: null)), ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Tarefa \"{result.Value!.Title}\" criada em {list.Name}{DuePt(context, due)}.",
            $"Task \"{result.Value!.Title}\" created in {list.Name}{DueEn(context, due)}."));
    }

    private static string DuePt(AssistantToolContext context, (DateTimeOffset At, bool HasTime)? due) =>
        due is { } d ? $" para {FormatDayOrInstant(context, d.At, d.HasTime)}" : "";

    private static string DueEn(AssistantToolContext context, (DateTimeOffset At, bool HasTime)? due) =>
        due is { } d ? $" due {FormatDayOrInstant(context, d.At, d.HasTime)}" : "";

    private static (string Title, (DateTimeOffset At, bool HasTime)? Due, TaskPriority Priority) Parse(
        AssistantToolContext context, JsonElement arguments)
    {
        var priority = OptionalString(arguments, "priority") is { } p
            ? Enum.TryParse<TaskPriority>(p, ignoreCase: true, out var parsed) ? parsed : TaskPriority.None
            : TaskPriority.None;
        return (RequiredString(arguments, "title"), OptionalDayOrInstant(context, arguments, "due"), priority);
    }
}
