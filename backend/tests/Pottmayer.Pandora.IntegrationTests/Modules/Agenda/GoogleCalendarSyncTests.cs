using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.BindGoogleCalendars;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncCalendarBinding;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendars;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvents;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// Google Calendar → Pandora against a real database, with an in-memory Google that honours sync tokens
/// the way the API does: a full listing, then only what changed (cancellations included), and 410 when
/// a token expires.
/// </summary>
[Collection("Integration")]
public sealed class GoogleCalendarSyncTests : IAsyncLifetime
{
    private const string Zone = "America/Sao_Paulo";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private readonly FakeGoogleCalendar _google = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    public GoogleCalendarSyncTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGoogleCalendarClient>();
            services.AddSingleton<IGoogleCalendarClient>(_google);
        }));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTimeOffset From = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 11, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Google_events_come_in_follow_changes_stay_read_only_and_survive_an_expired_token()
    {
        _google.Put("""
            { "id": "e1", "status": "confirmed", "summary": "Dentista", "etag": "1",
              "start": { "dateTime": "2026-11-10T09:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "end": { "dateTime": "2026-11-10T10:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "reminders": { "useDefault": true } }
            """);
        _google.Put("""
            { "id": "a1", "status": "confirmed", "summary": "Feriado",
              "start": { "date": "2026-11-15" }, "end": { "date": "2026-11-16" },
              "reminders": { "useDefault": false } }
            """);
        _google.Put("""
            { "id": "s1", "status": "confirmed", "summary": "Daily",
              "start": { "dateTime": "2026-11-02T09:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "end": { "dateTime": "2026-11-02T09:15:00-03:00", "timeZone": "America/Sao_Paulo" },
              "recurrence": [ "EXDATE;TZID=America/Sao_Paulo:20261104T090000", "RRULE:FREQ=WEEKLY;BYDAY=MO,WE;COUNT=6" ],
              "reminders": { "useDefault": false } }
            """);
        _google.Put("""
            { "id": "s1_20261109T120000Z", "status": "confirmed", "summary": "Daily (moved)", "recurringEventId": "s1",
              "originalStartTime": { "dateTime": "2026-11-09T09:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "start": { "dateTime": "2026-11-09T11:00:00-03:00" }, "end": { "dateTime": "2026-11-09T11:15:00-03:00" } }
            """);

        var binding = Assert.Single((await SendAsync(new BindGoogleCalendarsCommand(
            new BindGoogleCalendarsInput(_userId, _accountId, ["primary"])))).Value!);
        var synced = (await SendAsync(new SyncCalendarBindingNowCommand(new SyncCalendarBindingNowInput(_userId, binding.Id)))).Value;
        Assert.Equal("ok", synced.Status);

        var calendar = Assert.Single((await QueryAsync(new GetCalendarsQuery(new GetCalendarsInput(_userId)))).Value!);
        Assert.Equal("External", calendar.Origin);
        Assert.Equal("Work", calendar.Name);
        Assert.Equal(Zone, calendar.TimeZone);

        var occurrences = await OccurrencesAsync();
        var dentist = Assert.Single(occurrences, o => o.Title == "Dentista");
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero), dentist.StartsAt);
        Assert.Single(occurrences, o => o.Title == "Feriado" && o.IsAllDay);
        // Six Monday/Wednesday occurrences from 2 Nov, minus the excluded 4 Nov; 9 Nov moved to 11:00.
        var daily = occurrences.Where(o => o.EventId == occurrences.First(x => x.Title.StartsWith("Daily")).EventId).ToList();
        Assert.Equal(5, daily.Count);
        Assert.DoesNotContain(daily, o => o.StartsAt.Day == 4);
        var moved = Assert.Single(daily, o => o.StartsAt.Day == 9);
        Assert.Equal("Daily (moved)", moved.Title);
        Assert.Equal(14, moved.StartsAt.Hour);

        // The calendar's default reminder (30 min) became the dentist's alert; the others asked for none.
        Assert.Equal(new[] { -30 }, await AlertOffsetsAsync(dentist.EventId));

        // Read-only in Pandora.
        var edit = await SendAsync(new UpdateEventCommand(new UpdateEventInput(
            _userId, dentist.EventId, EventEditScope.All, null, "Mine", null, null, null, null, null, null, null)));
        Assert.Equal(CalendarErrors.ReadOnly.Code, Assert.Single(edit.Errors).Code);

        // Changes in Google: rename the dentist with its own reminder, cancel the holiday.
        _google.Put("""
            { "id": "e1", "status": "confirmed", "summary": "Dentista (retorno)", "etag": "2",
              "start": { "dateTime": "2026-11-10T09:00:00-03:00" }, "end": { "dateTime": "2026-11-10T10:00:00-03:00" },
              "reminders": { "useDefault": false, "overrides": [ { "method": "popup", "minutes": 10 } ] } }
            """);
        _google.Cancel("a1");
        await SendAsync(new SyncCalendarBindingCommand(new SyncCalendarBindingInput(binding.Id)));

        occurrences = await OccurrencesAsync();
        Assert.Contains(occurrences, o => o.Title == "Dentista (retorno)");
        Assert.DoesNotContain(occurrences, o => o.Title == "Feriado");
        Assert.Equal(new[] { -10 }, await AlertOffsetsAsync(dentist.EventId));

        // An expired token: a full listing again, nothing duplicated.
        _google.ExpireTokens();
        var again = (await SendAsync(new SyncCalendarBindingNowCommand(new SyncCalendarBindingNowInput(_userId, binding.Id)))).Value;
        Assert.Equal("ok", again.Status);
        Assert.Equal(occurrences.Count, (await OccurrencesAsync()).Count);

        // Unlinking keeps the calendar and its events, now as an ordinary editable calendar.
        Assert.True((await SendAsync(new UnlinkCalendarCommand(new UnlinkCalendarInput(_userId, binding.Id)))).IsSuccess);
        Assert.Equal("Local", Assert.Single((await QueryAsync(new GetCalendarsQuery(new GetCalendarsInput(_userId)))).Value!).Origin);
        var edited = await SendAsync(new UpdateEventCommand(new UpdateEventInput(
            _userId, dentist.EventId, EventEditScope.All, null, "Mine", null, null, null, null, null, null, null)));
        Assert.True(edited.IsSuccess);
    }

    [Fact]
    public async Task A_rule_pandora_cannot_expand_comes_as_the_instances_google_expands()
    {
        _google.Put("""
            { "id": "m1", "status": "confirmed", "summary": "Last Friday review",
              "start": { "dateTime": "2026-11-27T15:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "end": { "dateTime": "2026-11-27T16:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "recurrence": [ "RRULE:FREQ=MONTHLY;BYDAY=FR;BYSETPOS=-1" ] }
            """);
        _google.Instances["m1"] =
        [
            Event("""{ "id": "m1_20261127T180000Z", "status": "confirmed", "summary": "Last Friday review", "recurringEventId": "m1", "start": { "dateTime": "2026-11-27T15:00:00-03:00" }, "end": { "dateTime": "2026-11-27T16:00:00-03:00" } }"""),
            Event("""{ "id": "m1_20261225T180000Z", "status": "confirmed", "summary": "Last Friday review", "recurringEventId": "m1", "start": { "dateTime": "2026-12-25T15:00:00-03:00" }, "end": { "dateTime": "2026-12-25T16:00:00-03:00" } }"""),
        ];

        var binding = Assert.Single((await SendAsync(new BindGoogleCalendarsCommand(
            new BindGoogleCalendarsInput(_userId, _accountId, ["primary"])))).Value!);
        await SendAsync(new SyncCalendarBindingCommand(new SyncCalendarBindingInput(binding.Id)));

        Assert.Equal(2, await LiveEventRowsAsync());
        Assert.Single(await OccurrencesAsync(), o => o.Title == "Last Friday review");
    }

    [Fact]
    public async Task A_google_account_without_access_waits_for_a_reconnect()
    {
        var binding = Assert.Single((await SendAsync(new BindGoogleCalendarsCommand(
            new BindGoogleCalendarsInput(_userId, _accountId, ["primary"])))).Value!);

        _google.Failure = GoogleCalendarErrors.AuthFailed("refresh rejected");
        var result = (await SendAsync(new SyncCalendarBindingNowCommand(new SyncCalendarBindingNowInput(_userId, binding.Id)))).Value;

        Assert.Equal("needs-reconnect", result.Status);
        Assert.Contains("refresh rejected", result.LastError);
    }

    private static GoogleEvent Event(string json) =>
        JsonSerializer.Deserialize<GoogleEvent>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private async Task<IReadOnlyList<EventOccurrenceDto>> OccurrencesAsync() =>
        (await QueryAsync(new GetEventsQuery(new GetEventsInput(_userId, From, To, null)))).Value!;

    private async Task<int[]> AlertOffsetsAsync(Guid eventId)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT offset_minutes FROM agenda.agd007_alert WHERE subject_id = $1 ORDER BY offset_minutes";
        cmd.Parameters.AddWithValue(eventId);
        var offsets = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            offsets.Add(reader.GetInt32(0));
        return [.. offsets];
    }

    private async Task<int> LiveEventRowsAsync()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM agenda.agd002_event WHERE user_id = $1 AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue(_userId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command) where TResult : notnull
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, CancellationToken.None);
    }

    private async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query) where TResult : notnull
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(query, CancellationToken.None);
    }

    /// <summary>
    /// One Google calendar ("primary", "Work"), its events, and a change counter that stands for Google's
    /// sync token: a listing with a token returns what changed after it, cancellations included.
    /// </summary>
    private sealed class FakeGoogleCalendar : IGoogleCalendarClient
    {
        private readonly Dictionary<string, (GoogleEvent Event, int Version)> _events = [];
        private int _version;
        private bool _expired;

        public Dictionary<string, IReadOnlyList<GoogleEvent>> Instances { get; } = [];
        public Error? Failure { get; set; }

        private static readonly GoogleCalendarListEntry Calendar = new(
            "primary", "Work", null, "#039be5", Zone, "owner", true, [new GoogleReminder("popup", 30)]);

        public void Put(string json)
        {
            var ev = Event(json);
            _events[ev.Id] = (ev, ++_version);
        }

        public void Cancel(string id) => _events[id] = (_events[id].Event with { Status = "cancelled" }, ++_version);

        public void ExpireTokens() => _expired = true;

        public Task<Result<IReadOnlyList<GoogleCalendarListEntry>>> ListCalendarsAsync(Guid userId, Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(Failure is { } f
                ? Result<IReadOnlyList<GoogleCalendarListEntry>>.Failure([f])
                : Result<IReadOnlyList<GoogleCalendarListEntry>>.Success([Calendar]));

        public Task<Result<GoogleCalendarListEntry>> GetCalendarAsync(Guid userId, Guid accountId, string calendarId, CancellationToken ct = default) =>
            Task.FromResult(Failure is { } f ? Result<GoogleCalendarListEntry>.Failure([f]) : Result<GoogleCalendarListEntry>.Success(Calendar));

        public Task<Result<GoogleEventListing>> ListEventsAsync(
            Guid userId, Guid accountId, string calendarId, string? syncToken, DateTimeOffset? timeMin, CancellationToken ct = default)
        {
            if (Failure is { } f)
                return Task.FromResult(Result<GoogleEventListing>.Failure([f]));

            if (syncToken is not null && _expired)
            {
                _expired = false;
                return Task.FromResult(Result<GoogleEventListing>.Failure([GoogleCalendarErrors.SyncTokenExpired]));
            }

            var since = syncToken is null ? 0 : int.Parse(syncToken);
            var changed = _events.Values
                .Where(e => e.Version > since && (syncToken is not null || !e.Event.IsCancelled || e.Event.RecurringEventId is not null))
                .Select(e => e.Event)
                .ToList();
            return Task.FromResult(Result<GoogleEventListing>.Success(new GoogleEventListing(changed, _version.ToString())));
        }

        public Task<Result<IReadOnlyList<GoogleEvent>>> ListInstancesAsync(
            Guid userId, Guid accountId, string calendarId, string eventId, DateTimeOffset timeMin, DateTimeOffset timeMax,
            CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<GoogleEvent>>.Success(Instances.GetValueOrDefault(eventId, [])));
    }
}
