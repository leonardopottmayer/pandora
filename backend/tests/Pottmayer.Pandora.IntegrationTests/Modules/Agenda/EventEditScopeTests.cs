using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvents;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// The event edit scopes against a real database. Proves the Phase-4 acceptance criterion: a recurring
/// event edited with "this and future" splits, and the range expansion (the day view) agrees — the
/// occurrences before the cut keep the old series, the ones from the cut carry the new one, with no gap
/// and no duplicate at the boundary. Also covers the single-occurrence override (edit and cancel).
/// </summary>
[Collection("Integration")]
public sealed class EventEditScopeTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly Guid _userId = Guid.NewGuid();

    public EventEditScopeTests(PandoraWebApplicationFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTimeOffset Anchor = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowFrom = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowTo = new(2026, 6, 10, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task This_and_future_splits_the_series_and_the_day_view_agrees()
    {
        var calendarId = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Work", null, true, "UTC")))).Value.Id;

        var ev = (await SendAsync(new CreateEventCommand(new CreateEventInput(
            _userId, calendarId, "Daily standup", null, null, null, Anchor, Anchor.AddHours(1),
            IsAllDay: false, "UTC", "FREQ=DAILY", "Confirmed")))).Value;

        // Baseline: ten daily occurrences, all the original title.
        var before = (await QueryAsync(new GetEventsQuery(new GetEventsInput(_userId, WindowFrom, WindowTo, null)))).Value;
        Assert.Equal(10, before.Count);
        Assert.All(before, o => Assert.Equal("Daily standup", o.Title));

        // Split at the fourth occurrence (2026-06-04 09:00): rename this and every one after.
        var cut = Anchor.AddDays(3);
        var tail = (await SendAsync(new UpdateEventCommand(new UpdateEventInput(
            _userId, ev.Id, EventEditScope.ThisAndFuture, cut,
            Title: "New standup", Description: null, Location: null, Url: null,
            StartsAt: null, EndsAt: null, IsAllDay: null, CalendarId: null)))).Value;

        // Two event rows now exist: the truncated original and the new tail.
        Assert.NotEqual(ev.Id, tail.Id);
        Assert.Equal(2, await EventRowCountAsync());

        // The expansion still covers the same ten days — no gap, no duplicate at the boundary — but the
        // title flips exactly at the cut.
        var after = (await QueryAsync(new GetEventsQuery(new GetEventsInput(_userId, WindowFrom, WindowTo, null)))).Value;
        Assert.Equal(10, after.Count);
        Assert.All(after, o => Assert.Equal(
            o.StartsAt < cut ? "Daily standup" : "New standup", o.Title));

        // The day view for a single day on each side agrees with the split.
        var dayBefore = (await QueryAsync(new GetEventsQuery(new GetEventsInput(
            _userId, Anchor.AddDays(1), Anchor.AddDays(1).AddHours(2), null)))).Value;
        Assert.Equal("Daily standup", Assert.Single(dayBefore).Title);

        var dayAfter = (await QueryAsync(new GetEventsQuery(new GetEventsInput(
            _userId, cut, cut.AddHours(2), null)))).Value;
        Assert.Equal("New standup", Assert.Single(dayAfter).Title);
    }

    [Fact]
    public async Task This_scope_writes_an_override_that_cancels_or_edits_one_occurrence()
    {
        var calendarId = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Work", null, true, "UTC")))).Value.Id;
        var ev = (await SendAsync(new CreateEventCommand(new CreateEventInput(
            _userId, calendarId, "Daily standup", null, null, null, Anchor, Anchor.AddHours(1),
            false, "UTC", "FREQ=DAILY", "Confirmed")))).Value;

        // Edit only the second occurrence.
        var editDay = Anchor.AddDays(1);
        await SendAsync(new UpdateEventCommand(new UpdateEventInput(
            _userId, ev.Id, EventEditScope.This, editDay,
            Title: "Moved", Description: null, Location: "Room 9", Url: null,
            StartsAt: editDay.AddHours(2), EndsAt: editDay.AddHours(3), IsAllDay: null, CalendarId: null)));

        // Cancel the third occurrence.
        var cancelDay = Anchor.AddDays(2);
        await SendAsync(new DeleteEventCommand(new DeleteEventInput(
            _userId, ev.Id, EventEditScope.This, cancelDay)));

        var occ = (await QueryAsync(new GetEventsQuery(new GetEventsInput(_userId, WindowFrom, WindowTo, null)))).Value;

        // Ten days minus the cancelled one.
        Assert.Equal(9, occ.Count);
        Assert.DoesNotContain(occ, o => o.OriginalStartsAt == cancelDay);

        var edited = Assert.Single(occ, o => o.OriginalStartsAt == editDay);
        Assert.Equal("Moved", edited.Title);
        Assert.Equal("Room 9", edited.Location);
        Assert.Equal(editDay.AddHours(2), edited.StartsAt);
        // The series is untouched for other days.
        Assert.Equal("Daily standup", occ.First(o => o.OriginalStartsAt == Anchor).Title);
    }

    // ── helpers ──

    private async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command)
        where TResult : notnull
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command, CancellationToken.None);
    }

    private async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query)
        where TResult : notnull
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(query, CancellationToken.None);
    }

    private async Task<int> EventRowCountAsync()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM agenda.agd002_event WHERE user_id = $1 AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue(_userId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
