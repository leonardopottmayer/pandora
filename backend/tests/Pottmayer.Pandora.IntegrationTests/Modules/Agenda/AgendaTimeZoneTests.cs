using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvents;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetToday;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// Days are the viewer's days: a timed item lands on the day its instant falls on in the viewer's zone,
/// and a date-only item stays on its own date whatever zone it was set in or is viewed from.
/// The clock is 12:00 on Nov 13 in São Paulo (15:00 UTC).
/// </summary>
[Collection("Integration")]
public sealed class AgendaTimeZoneTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Nov13 = new(2026, 11, 13);
    private static readonly DateOnly Nov14 = new(2026, 11, 14);

    private readonly PandoraWebApplicationFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private readonly ViewerZone _zone = new() { Zone = "America/Sao_Paulo" };
    private readonly Guid _userId = Guid.NewGuid();

    public AgendaTimeZoneTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<TimeProvider>(new FixedClock(Now));
            s.AddSingleton<IEffectiveTimeZoneResolver>(_zone);
        }));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_task_due_at_02_00_utc_on_the_14th_is_on_the_13th_for_a_sao_paulo_user()
    {
        var list = await ListAsync();
        await TaskAsync(list, "Late call", new DateTimeOffset(2026, 11, 14, 2, 0, 0, TimeSpan.Zero), hasTime: true);

        Assert.Equal(["Late call"], await TodayTitlesAsync(Nov13));
        Assert.Empty(await TodayTitlesAsync(Nov14));
        Assert.Equal(["Late call"], (await TasksAsync(TaskDueBucket.Today)).Select(t => t.Title));
    }

    [Fact]
    public async Task A_date_only_task_from_the_web_lands_on_the_picked_date_even_late_at_night()
    {
        // The picker sends Nov 14 with the clock time it was picked at: 23:30 in São Paulo (02:30 UTC Nov 15).
        var list = await ListAsync();
        await TaskAsync(list, "Pay rent", new DateTimeOffset(2026, 11, 14, 23, 30, 0, TimeSpan.FromHours(-3)), hasTime: false);

        Assert.Equal(["Pay rent"], await TodayTitlesAsync(Nov14));
        var task = Assert.Single(await TasksAsync(TaskDueBucket.Week));
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.FromHours(-3)), task.DueAt);
        Assert.Empty(await TasksAsync(TaskDueBucket.Today));
    }

    [Fact]
    public async Task A_date_only_task_set_in_tokyo_stays_on_its_date_for_viewers_anywhere()
    {
        var list = await ListAsync();
        await TaskAsync(list, "Visa form", new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.FromHours(9)), hasTime: false, zone: "Asia/Tokyo");

        // Stored as Nov 13 15:00 UTC — inside São Paulo's Nov 13 — but it is a Nov 14 task.
        Assert.Empty(await TodayTitlesAsync(Nov13));
        Assert.Equal(["Visa form"], await TodayTitlesAsync(Nov14));

        _zone.Zone = "America/Los_Angeles";
        Assert.Equal(["Visa form"], await TodayTitlesAsync(Nov14));
        var task = Assert.Single(await TasksAsync(null));
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.FromHours(-8)), task.DueAt);
    }

    [Fact]
    public async Task Editing_a_date_only_task_from_another_zone_keeps_its_date()
    {
        var list = await ListAsync();
        var created = await TaskAsync(list, "Visa form", new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.FromHours(9)), hasTime: false, zone: "Asia/Tokyo");

        // A São Paulo viewer renames it, sending back the due they were shown (Nov 14 00:00 -03:00).
        var shown = Assert.Single(await TasksAsync(null));
        Assert.True((await SendAsync(new UpdateTaskCommand(new UpdateTaskInput(
            _userId, created.Id, "Visa form (signed)", null, shown.DueAt, false, TaskPriority.None)))).IsSuccess);

        _zone.Zone = "Asia/Tokyo";
        var task = Assert.Single(await TasksAsync(null));
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.FromHours(9)), task.DueAt);
    }

    [Fact]
    public async Task An_all_day_event_in_a_tokyo_calendar_shows_on_its_date_in_sao_paulo()
    {
        var calendar = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Japan holidays", null, true, "Asia/Tokyo")))).Value!.Id;
        var start = new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.FromHours(9));
        Assert.True((await SendAsync(new CreateEventCommand(new CreateEventInput(
            _userId, calendar, "Holiday", null, null, null, start, start.AddDays(1), true, "Asia/Tokyo", null, "Confirmed")))).IsSuccess);

        var spDay = (DateOnly d) => new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-3));
        Assert.Empty(await EventsAsync(spDay(Nov13), spDay(Nov14).AddTicks(-1)));
        var occ = Assert.Single(await EventsAsync(spDay(Nov14), spDay(Nov14.AddDays(1)).AddTicks(-1)));
        Assert.Equal(spDay(Nov14), occ.StartsAt);
        Assert.Equal(spDay(Nov14.AddDays(1)), occ.EndsAt);

        Assert.Empty(await TodayTitlesAsync(Nov13));
        Assert.Equal(["Holiday"], await TodayTitlesAsync(Nov14));
    }

    [Fact]
    public async Task A_timed_event_follows_the_viewers_clock()
    {
        var calendar = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Work", null, true, "Asia/Tokyo")))).Value!.Id;
        // 10:00 Nov 14 in Tokyo is 22:00 Nov 13 in São Paulo.
        var start = new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.FromHours(9));
        await SendAsync(new CreateEventCommand(new CreateEventInput(
            _userId, calendar, "Standup", null, null, null, start, start.AddHours(1), false, "Asia/Tokyo", null, "Confirmed")));

        Assert.Equal(["Standup"], await TodayTitlesAsync(Nov13));
        Assert.Empty(await TodayTitlesAsync(Nov14));
    }

    // ── helpers ──

    private async Task<Guid> ListAsync() =>
        (await SendAsync(new CreateTaskListCommand(new CreateTaskListInput(_userId, "Inbox", true)))).Value!.Id;

    private async Task<TaskDto> TaskAsync(Guid list, string title, DateTimeOffset due, bool hasTime, string? zone = null)
    {
        var result = await SendAsync(new CreateTaskCommand(new CreateTaskInput(
            _userId, list, null, title, null, due, hasTime, TaskPriority.None, zone, null)));
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private async Task<List<string>> TodayTitlesAsync(DateOnly day) =>
        [.. (await QueryAsync(new GetTodayQuery(new GetTodayInput(_userId, day, day)))).Value!.Select(i => i.Title)];

    private async Task<IReadOnlyList<TaskDto>> TasksAsync(TaskDueBucket? bucket) =>
        (await QueryAsync(new GetTasksQuery(new GetTasksInput(_userId, null, null, bucket)))).Value!;

    private async Task<IReadOnlyList<EventOccurrenceDto>> EventsAsync(DateTimeOffset from, DateTimeOffset to) =>
        (await QueryAsync(new GetEventsQuery(new GetEventsInput(_userId, from, to, null)))).Value!;

    private async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command)
        where TResult : notnull
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, CancellationToken.None);
    }

    private async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query)
        where TResult : notnull
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(query, CancellationToken.None);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The viewer's zone, switchable mid-test; an explicit zone on a create still wins.</summary>
    private sealed class ViewerZone : IEffectiveTimeZoneResolver
    {
        public string Zone { get; set; } = "UTC";

        public Task<TimeZoneInfo> ResolveAsync(Guid userId, string? requested = null, CancellationToken ct = default) =>
            Task.FromResult(TimeZoneInfo.FindSystemTimeZoneById(requested ?? Zone));

        public TimeZoneInfo ResolveDefault() => TimeZoneInfo.FindSystemTimeZoneById(Zone);
    }
}
