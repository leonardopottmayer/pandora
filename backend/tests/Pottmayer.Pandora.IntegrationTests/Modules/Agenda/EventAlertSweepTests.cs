using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateAlert;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Sweep;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// The event-alert slice against a real database. Proves the Phase-4 acceptance criterion: an event
/// alert fires exactly once per occurrence, re-running the sweep over the same tick writes no duplicate
/// (the UNIQUE (alert, occurrence) guard), and a recurring event whose anchor predates a daylight-saving
/// transition still fires today's occurrence at the correct DST-aware instant.
///
/// <para>The deterministic "23 hours across the spring-forward" wall-clock guarantee is proven at the
/// engine level in <c>EventTests</c> (pinned to March); here the sweep runs on the real clock, so the
/// DST case anchors a daily series 300 days in the past — a span that always crosses at least one US
/// transition — and asserts the occurrence fired now lands on the expected UTC instant.</para>
/// </summary>
[Collection("Integration")]
public sealed class EventAlertSweepTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly string _conn;
    private readonly Guid _userId = Guid.NewGuid();
    private const string ChatId = "555000444";

    public EventAlertSweepTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _conn = factory.ConnectionString;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_event_alert_fires_once_and_a_second_sweep_over_the_same_tick_does_not_duplicate()
    {
        await LinkTelegramAsync(_userId, ChatId);

        var calendarId = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Work", null, true, "UTC")))).Value.Id;

        // Started five minutes ago with a zero offset: the firing anchor sits inside the grace window.
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        var ev = (await SendAsync(new CreateEventCommand(new CreateEventInput(
            _userId, calendarId, "Kickoff", null, "Room 1", "https://meet.example", start, start.AddHours(1),
            IsAllDay: false, "UTC", Rrule: null, "Confirmed")))).Value;
        await SendAsync(new CreateAlertCommand(new CreateAlertInput(_userId, "event", ev.Id, OffsetMinutes: 0, Channels: null)));

        var first = await SweepAsync();
        Assert.True(first.IsSuccess);
        Assert.True(first.Value >= 1);
        Assert.Equal(1, await DispatchCountAsync());

        // The notification went out on the event template, carrying the event's payload. (Event alerts
        // have no buttons, so Telegram's rendered_payload stays null — the text is rendered at send time.)
        var (template, payload) = await LatestTelegramNotificationAsync(ChatId);
        Assert.Equal("agenda.event.due", template);
        Assert.Contains("Kickoff", payload);

        // Re-running the sweep over the same tick is idempotent: no second dispatch row.
        var second = await SweepAsync();
        Assert.True(second.IsSuccess);
        Assert.Equal(1, await DispatchCountAsync());
    }

    [Fact]
    public async Task A_recurring_event_alert_fires_todays_occurrence_with_dst_aware_expansion()
    {
        await LinkTelegramAsync(_userId, ChatId);
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        // Want an occurrence ~5 minutes ago (inside the grace window), on whole seconds so it round-trips.
        var raw = DateTimeOffset.UtcNow.AddMinutes(-5);
        var occUtc = new DateTimeOffset(raw.Ticks - raw.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var occNy = TimeZoneInfo.ConvertTime(occUtc, ny);

        // Anchor the daily series 300 days earlier at the same wall-clock time — a span that always
        // crosses at least one DST transition, so today's occurrence is expanded with a different offset.
        var anchorLocal = occNy.DateTime.AddDays(-300);
        var anchorStart = new DateTimeOffset(anchorLocal, ny.GetUtcOffset(anchorLocal));

        var calendarId = (await SendAsync(new CreateCalendarCommand(
            new CreateCalendarInput(_userId, "Personal", null, true, "America/New_York")))).Value.Id;
        var ev = (await SendAsync(new CreateEventCommand(new CreateEventInput(
            _userId, calendarId, "Meds", null, null, null, anchorStart, anchorStart.AddHours(1),
            IsAllDay: false, "America/New_York", "FREQ=DAILY", "Confirmed")))).Value;
        await SendAsync(new CreateAlertCommand(new CreateAlertInput(_userId, "event", ev.Id, OffsetMinutes: 0, Channels: null)));

        var first = await SweepAsync();
        Assert.True(first.IsSuccess);
        Assert.Equal(1, await DispatchCountAsync());

        // Exactly today's occurrence fired — the DST-aware expansion converted today's wall clock with
        // today's offset, not the (months-old) anchor's.
        Assert.Equal(occUtc, await DispatchedOccurrenceAsync());

        // Idempotent across a re-run over the same tick.
        await SweepAsync();
        Assert.Equal(1, await DispatchCountAsync());
    }

    // ── helpers ──

    private async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command)
        where TResult : notnull
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command, CancellationToken.None);
    }

    private async Task<Result<int>> SweepAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new DispatchDueEventAlertsCommand(new DispatchDueEventAlertsInput(BatchSize: 50)), CancellationToken.None);
        await _factory.DrainOutboxAsync(); // deliver the NotifyUserRequested events the sweep parked in the outbox
        return result;
    }

    private Task LinkTelegramAsync(Guid userId, string chatId) => ExecuteAsync("""
        INSERT INTO channels.chn001_user_channel
            (user_id, channel, address, locale, is_verified, verified_at, is_enabled, metadata)
        VALUES ($1, 'telegram', $2, 'pt-BR', true, current_timestamp, true, '{}'::jsonb)
        """, cmd =>
    {
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(chatId);
    });

    private async Task<int> DispatchCountAsync() =>
        Convert.ToInt32(await ScalarAsync("SELECT count(*) FROM agenda.agd008_alert_dispatch WHERE user_id = $1",
            cmd => cmd.Parameters.AddWithValue(_userId)));

    private async Task<DateTimeOffset> DispatchedOccurrenceAsync()
    {
        var value = await ScalarAsync(
            "SELECT occurrence_starts_at FROM agenda.agd008_alert_dispatch WHERE user_id = $1",
            cmd => cmd.Parameters.AddWithValue(_userId));
        var utc = DateTime.SpecifyKind((DateTime)value!, DateTimeKind.Utc);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private async Task<(string Template, string Payload)> LatestTelegramNotificationAsync(string chatId)
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT template_key, payload::text
            FROM channels.chn006_notification
            WHERE channel = 'telegram' AND recipient = $1
            ORDER BY created_at DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue(chatId);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "no telegram notification was queued");
        return (reader.GetString(0), reader.GetString(1));
    }

    private async Task ExecuteAsync(string sql, Action<NpgsqlCommand> bind)
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind(cmd);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql, Action<NpgsqlCommand> bind)
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        bind(cmd);
        return await cmd.ExecuteScalarAsync();
    }
}
