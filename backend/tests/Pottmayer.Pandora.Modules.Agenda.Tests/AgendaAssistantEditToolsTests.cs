using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Assistant;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CancelReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.RenameReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

/// <summary>The assistant tools that list tasks and change or delete agenda items.</summary>
public sealed class AgendaAssistantEditToolsTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", SaoPaulo);

    // Friday 2026-09-04, 12:00 in São Paulo (UTC-3).
    private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 9, 4, 15, 0, 0, TimeSpan.Zero));

    /// <summary>Answers each request with the scripted response of its type, and records what was sent.</summary>
    private sealed class ScriptedSender(params object[] responses) : ISender
    {
        public List<object> Sent { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return ValueTask.FromResult(responses.OfType<TResponse>().First());
        }
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>The arguments as the pipeline hands them over after pinning <c>ref</c> to <paramref name="listing"/>.</summary>
    private static JsonElement Pinned(string json, params ListedItem[] listing) => ListedRefs.Pin(Args(json), listing);

    private static Result<IReadOnlyList<T>> List<T>(params T[] items) => Result<IReadOnlyList<T>>.Success(items);

    private static TaskDto Task(string title, string? due = null, bool hasTime = false, string status = "Todo", Guid? listId = null) =>
        new(Guid.NewGuid(), listId ?? Guid.Empty, null, title, null, due is null ? null : DateTimeOffset.Parse(due), hasTime,
            "Medium", status, null, "UTC", null, 0);

    private static EventOccurrenceDto Occurrence(Guid eventId, string title, string startsAt, int hours = 1)
    {
        var start = DateTimeOffset.Parse(startsAt);
        return new(eventId, Guid.NewGuid(), start, start, start.AddHours(hours), false, title, null, null, null, "Confirmed");
    }

    private static EventDto Series(Guid id, string? rrule) =>
        new(id, Guid.NewGuid(), "x", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false, "UTC", rrule, null, "Confirmed");

    [Fact]
    public async Task List_tasks_numbers_the_open_overdue_ones_in_the_users_zone()
    {
        var list = new TaskListDto(Guid.NewGuid(), "Entrada", true, 0, null);
        var late = Task("Pagar luz", "2026-09-03T00:00:00-03:00", listId: list.Id);
        var sender = new ScriptedSender(
            List(list),
            List(
                Task("Hoje", "2026-09-04T00:00:00-03:00", listId: list.Id),
                late,
                Task("Já foi às 9", "2026-09-04T09:00:00-03:00", hasTime: true, listId: list.Id),
                Task("Feita e atrasada", "2026-09-01T00:00:00-03:00", status: "Done", listId: list.Id)));

        var outcome = await new ListTasksTool(sender, Clock).ExecuteAsync(Context, Args("""{ "due": "overdue" }"""));

        Assert.Equal(
            "Suas tarefas:\n1. Pagar luz · 03/09/2026 (atrasada)\n2. Já foi às 9 · 04/09/2026 às 09:00 (atrasada)",
            outcome.Message);
        Assert.Equal([late.Id], outcome.Listed!.Take(1).Select(l => l.Id));
        Assert.All(outcome.Listed!, l => Assert.Equal("task", l.Kind));
        Assert.DoesNotContain("luz", outcome.Recap);
    }

    [Fact]
    public async Task Update_event_moves_the_named_occurrence_of_a_series_keeping_its_length()
    {
        var eventId = Guid.NewGuid();
        var sender = new ScriptedSender(
            List(Occurrence(eventId, "Dentista", "2026-09-10T09:00:00-03:00", hours: 2),
                Occurrence(eventId, "Dentista", "2026-09-17T09:00:00-03:00", hours: 2)),
            Result<EventDto>.Success(Series(eventId, "FREQ=WEEKLY")));

        var outcome = await new UpdateEventTool(sender, Clock).ExecuteAsync(
            Context, Args("""{ "event": "dentista", "startsAt": "2026-09-10T15:00:00-03:00" }"""));

        var input = sender.Sent.OfType<UpdateEventCommand>().Single().Input;
        Assert.Equal(EventEditScope.This, input.Scope);
        Assert.Equal(DateTimeOffset.Parse("2026-09-10T09:00:00-03:00"), input.OccurrenceStart);
        Assert.Equal(TimeSpan.FromHours(2), input.EndsAt - input.StartsAt);
        Assert.Equal("Evento \"Dentista\" (10/09/2026 às 09:00) alterado: para 10/09/2026 às 15:00.", outcome.Message);
    }

    [Fact]
    public async Task Update_event_with_only_a_time_keeps_the_occurrences_day_and_length()
    {
        var eventId = Guid.NewGuid();
        var sender = new ScriptedSender(
            List(Occurrence(eventId, "Jantar da Ana", "2026-09-08T19:30:00-03:00", hours: 2)),
            Result<EventDto>.Success(Series(eventId, rrule: null)));

        var outcome = await new UpdateEventTool(sender, Clock).ExecuteAsync(
            Context, Args("""{ "event": "jantar", "time": "20:00" }"""));

        var input = sender.Sent.OfType<UpdateEventCommand>().Single().Input;
        Assert.Equal(DateTimeOffset.Parse("2026-09-08T20:00:00-03:00"), input.StartsAt);
        Assert.Equal(DateTimeOffset.Parse("2026-09-08T22:00:00-03:00"), input.EndsAt);
        Assert.Equal("Evento \"Jantar da Ana\" (08/09/2026 às 19:30) alterado: para as 20:00.", outcome.Message);
    }

    [Fact]
    public async Task Delete_event_by_number_removes_a_one_off_event_whole()
    {
        var eventId = Guid.NewGuid();
        var at = DateTimeOffset.Parse("2026-09-05T09:00:00-03:00");
        var sender = new ScriptedSender(
            List(Occurrence(eventId, "Dentista", "2026-09-05T09:00:00-03:00")),
            Result<EventDto>.Success(Series(eventId, rrule: null)),
            Result<bool>.Success(true));
        var arguments = Pinned("""{ "ref": 1 }""", new ListedItem("event", eventId, "Dentista", at));
        var tool = new DeleteEventTool(sender, Clock);

        Assert.Equal("Excluir o evento \"Dentista (05/09/2026 às 09:00)\"?", tool.Describe(Context, arguments));
        var outcome = await tool.ExecuteAsync(Context, arguments);

        var input = sender.Sent.OfType<DeleteEventCommand>().Single().Input;
        Assert.Equal((eventId, EventEditScope.All, (DateTimeOffset?)null), (input.EventId, input.Scope, input.OccurrenceStart));
        Assert.True(outcome.Success);
        Assert.Equal(ConfirmationPolicy.Required, tool.Descriptor.Confirmation);
    }

    [Fact]
    public async Task Update_task_changes_only_what_was_said()
    {
        var task = Task("Renovar passaporte", "2026-09-11T00:00:00-03:00");
        var sender = new ScriptedSender(List(task), Result<TaskDto>.Success(task));

        await new UpdateTaskTool(sender).ExecuteAsync(Context, Args("""{ "task": "passaporte", "due": "2026-09-14" }"""));

        var input = sender.Sent.OfType<UpdateTaskCommand>().Single().Input;
        Assert.Equal(("Renovar passaporte", TaskPriority.Medium, false), (input.Title, input.Priority, input.DueHasTime));
        Assert.Equal(new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.FromHours(-3)), input.DueAt);
    }

    [Fact]
    public async Task Delete_task_by_number_deletes_the_pinned_task_and_refuses_a_number_that_is_not_a_task()
    {
        var task = Task("Pagar luz");
        var sender = new ScriptedSender(List(Task("Outra"), task), Result<bool>.Success(true));
        var listing = new[] { new ListedItem("event", Guid.NewGuid(), "Dentista"), new ListedItem("task", task.Id, "Pagar luz") };
        var tool = new DeleteTaskTool(sender);

        var outcome = await tool.ExecuteAsync(Context, Pinned("""{ "ref": 2 }""", listing));
        var wrongKind = await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync(Context, Pinned("""{ "ref": 1 }""", listing)));

        Assert.Equal("Tarefa \"Pagar luz\" excluída.", outcome.Message);
        Assert.Equal(task.Id, sender.Sent.OfType<DeleteTaskCommand>().Single().Input.TaskId);
        Assert.Equal("O item 1 (\"Dentista\") não é uma tarefa.", wrongKind.Message);
    }

    [Fact]
    public async Task Cancel_reminder_finds_the_pending_one_by_name()
    {
        var rent = new ReminderDto(Guid.NewGuid(), "Pagar aluguel", null, DateTimeOffset.UtcNow, "UTC", null, null, "Scheduled", null, null);
        var sender = new ScriptedSender(List(rent), Result<bool>.Success(true));

        var outcome = await new CancelReminderTool(sender).ExecuteAsync(Context, Args("""{ "reminder": "aluguel" }"""));

        Assert.Equal("Lembrete \"Pagar aluguel\" cancelado.", outcome.Message);
        Assert.Equal(rent.Id, sender.Sent.OfType<CancelReminderCommand>().Single().Input.ReminderId);
    }

    [Fact]
    public async Task List_reminders_numbers_the_pending_ones_one_offs_first_by_when_they_fire()
    {
        ReminderDto R(string title, string at, string status = "Scheduled", string? rrule = null, string? snoozed = null) =>
            new(Guid.NewGuid(), title, null, DateTimeOffset.Parse(at), "UTC", rrule, null, status,
                snoozed is null ? null : DateTimeOffset.Parse(snoozed), null);
        var sender = new ScriptedSender(List(
            R("Remédio", "2026-09-27T21:00:00-03:00", rrule: "FREQ=DAILY"),
            R("Ligar pra mãe", "2026-10-05T19:00:00-03:00"),
            R("Revisão do carro", "2026-10-09T08:00:00-03:00", status: "Snoozed", snoozed: "2026-10-03T08:00:00-03:00"),
            R("Antigo", "2026-09-20T09:00:00-03:00", status: "Acknowledged")));

        var outcome = await new ListRemindersTool(sender).ExecuteAsync(Context, Args("{}"));

        Assert.Equal(
            "Seus lembretes:\n1. 03/10/2026 às 08:00 · Revisão do carro\n2. 05/10/2026 às 19:00 · Ligar pra mãe\n3. Remédio · repete, às 21:00",
            outcome.Message);
        Assert.Equal(["Revisão do carro", "Ligar pra mãe", "Remédio"], outcome.Listed!.Select(l => l.Label));
    }

    [Fact]
    public async Task A_targeted_tool_finds_its_item_by_name_so_the_confirmation_names_it_as_it_is()
    {
        var task = Task("Organizar fotos de 2025");
        var sender = new ScriptedSender(List(task));
        var tool = new DeleteTaskTool(sender);
        var arguments = Args("""{ "task": "organizar fotos" }""");

        var (target, problem) = await tool.FindTargetAsync(Context, arguments);
        var (missing, why) = await tool.FindTargetAsync(Context, Args("""{ "task": "reforma" }"""));

        Assert.Null(problem);
        Assert.Equal("Excluir a tarefa \"Organizar fotos de 2025\"?", tool.Describe(Context, ListedRefs.PinTarget(arguments, target!)));
        Assert.Null(missing);
        Assert.Equal("Não encontrei \"reforma\" entre suas tarefas.", why);
    }

    [Fact]
    public async Task Rename_reminder_by_number_sends_the_new_title_for_the_pinned_reminder()
    {
        var rent = new ReminderDto(Guid.NewGuid(), "Pagar aluguel", null, DateTimeOffset.UtcNow, "UTC", null, null, "Scheduled", null, null);
        var sender = new ScriptedSender(List(rent), Result<bool>.Success(true));
        var arguments = Pinned("""{ "ref": 1, "title": "Pagar aluguel e condomínio" }""",
            new ListedItem("reminder", rent.Id, rent.Title));
        var tool = new RenameReminderTool(sender);

        Assert.Equal("Renomear o lembrete \"Pagar aluguel\" para \"Pagar aluguel e condomínio\"?", tool.Describe(Context, arguments));
        var outcome = await tool.ExecuteAsync(Context, arguments);

        var input = sender.Sent.OfType<RenameReminderCommand>().Single().Input;
        Assert.Equal((rent.Id, "Pagar aluguel e condomínio"), (input.ReminderId, input.Title));
        Assert.Equal("Lembrete \"Pagar aluguel\" renomeado para \"Pagar aluguel e condomínio\".", outcome.Message);
    }
}
