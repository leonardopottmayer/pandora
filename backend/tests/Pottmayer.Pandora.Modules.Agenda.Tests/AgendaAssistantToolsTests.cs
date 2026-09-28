using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Assistant;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class AgendaAssistantToolsTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", SaoPaulo);

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

    private static TaskDto Task(string title, string status = "Todo") =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, title, null, null, false, "None", status, null, "UTC", null, 0);

    private static ReminderDto Reminder(string title, string? rrule = null) =>
        new(Guid.NewGuid(), title, null, DateTimeOffset.UtcNow, "UTC", rrule, null, "Scheduled", null, null);

    private static Result<IReadOnlyList<T>> List<T>(params T[] items) => Result<IReadOnlyList<T>>.Success(items);

    [Theory]
    [InlineData("Pagar o aluguel", "pagar o aluguel")]      // exact, case-insensitive
    [InlineData("Revisão do carro", "revisao carro")]       // accents and short words ignored
    [InlineData("Comprar tinta da impressora", "impressora")] // one word of the title
    [InlineData("Comprar tinta da impressora", "tinta impr")] // word prefixes
    public void Matches_the_title_the_user_said(string title, string query) =>
        Assert.Single(ToolArguments.MatchByTitle([title, "Outra coisa"], t => t, query));

    [Fact]
    public void An_exact_title_wins_over_titles_that_only_contain_it() =>
        Assert.Equal(["Aluguel"], ToolArguments.MatchByTitle(["Aluguel", "Aluguel do escritório"], t => t, "aluguel"));

    [Fact]
    public async Task Complete_task_finds_the_open_task_by_the_users_words()
    {
        var rent = Task("Pagar o aluguel");
        var sender = new ScriptedSender(
            List(Task("Pagar o aluguel antigo", "Done"), rent, Task("Renovar passaporte")),
            Result<TaskDto>.Success(rent));

        var outcome = await new CompleteTaskTool(sender).ExecuteAsync(Context, Args("""{ "task": "aluguel" }"""));

        Assert.True(outcome.Success);
        Assert.Equal("Tarefa \"Pagar o aluguel\" concluída.", outcome.Message);
        Assert.Equal(rent.Id, sender.Sent.OfType<CompleteTaskCommand>().Single().Input.TaskId);
    }

    [Fact]
    public async Task Complete_task_asks_which_one_when_several_match_and_completes_nothing()
    {
        var sender = new ScriptedSender(List(Task("Pagar o aluguel"), Task("Cobrar o aluguel")));

        var outcome = await new CompleteTaskTool(sender).ExecuteAsync(Context, Args("""{ "task": "aluguel" }"""));

        Assert.False(outcome.Success);
        Assert.Contains("\"Pagar o aluguel\", \"Cobrar o aluguel\"", outcome.Message);
        Assert.Empty(sender.Sent.OfType<CompleteTaskCommand>());
    }

    [Fact]
    public async Task Snooze_moves_a_one_off_reminder_and_refuses_a_recurring_one()
    {
        var dentist = Reminder("Dentista");
        var until = "2026-09-05T13:00:00Z";
        var sender = new ScriptedSender(List(dentist, Reminder("Remédio", "FREQ=DAILY")), Result<bool>.Success(true));
        var tool = new SnoozeReminderTool(sender);

        var ok = await tool.ExecuteAsync(Context, Args($$"""{ "reminder": "dentista", "until": "{{until}}" }"""));
        var refused = await tool.ExecuteAsync(Context, Args($$"""{ "reminder": "remedio", "until": "{{until}}" }"""));

        Assert.Equal("Lembrete \"Dentista\" adiado para 05/09/2026 às 10:00.", ok.Message);
        Assert.False(refused.Success);
        var snoozed = Assert.Single(sender.Sent.OfType<SnoozeReminderCommand>());
        Assert.Equal(dentist.Id, snoozed.Input.ReminderId);
    }

    [Fact]
    public async Task Create_task_goes_to_the_default_list_with_a_bare_due_day()
    {
        var inbox = new TaskListDto(Guid.NewGuid(), "Entrada", IsDefault: true, 1, null);
        var sender = new ScriptedSender(
            List(new TaskListDto(Guid.NewGuid(), "Trabalho", false, 0, null), inbox),
            Result<TaskDto>.Success(Task("Renovar passaporte")));

        var outcome = await new CreateTaskTool(sender).ExecuteAsync(
            Context, Args("""{ "title": "Renovar passaporte", "due": "2026-09-11" }"""));

        var input = sender.Sent.OfType<CreateTaskCommand>().Single().Input;
        Assert.Equal(inbox.Id, input.ListId);
        Assert.Equal(new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.FromHours(-3)), input.DueAt);
        Assert.False(input.DueHasTime);
        Assert.Equal("Tarefa \"Renovar passaporte\" criada em Entrada para 11/09/2026.", outcome.Message);
    }

    [Theory]
    [InlineData("2026-09-08T15:00:00-03:00", false, 1)]
    [InlineData("2026-09-12", true, 24)]
    public async Task Create_event_lasts_an_hour_or_the_whole_day(string startsAt, bool allDay, int hours)
    {
        var calendar = new CalendarDto(Guid.NewGuid(), "Pessoal", null, true, true, "UTC", "local", null);
        var created = new EventDto(Guid.NewGuid(), calendar.Id, "Dentista", null, null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, allDay, "UTC", null, null, "Confirmed");
        var sender = new ScriptedSender(List(calendar), Result<EventDto>.Success(created));

        await new CreateEventTool(sender).ExecuteAsync(Context, Args($$"""{ "title": "Dentista", "startsAt": "{{startsAt}}" }"""));

        var input = sender.Sent.OfType<CreateEventCommand>().Single().Input;
        Assert.Equal(calendar.Id, input.CalendarId);
        Assert.Equal(allDay, input.IsAllDay);
        Assert.Equal(TimeSpan.FromHours(hours), input.EndsAt - input.StartsAt);
    }
}
