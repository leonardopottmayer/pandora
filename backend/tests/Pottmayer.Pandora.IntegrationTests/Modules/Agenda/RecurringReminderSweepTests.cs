using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Sweep;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Messaging.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// The recurring-reminder slice against a real database, proving the per-occurrence dispatch ledger:
/// an occurrence fires exactly once, re-running the sweep over the same tick writes no duplicate (the
/// UNIQUE (reminder, occurrence) guard), the reminder stays scheduled so the series lives on, and an
/// inline tap acknowledges just that occurrence.
///
/// <para>The daylight-saving "exactly once per business day across the transition" guarantee is proven
/// deterministically at the engine level in <c>RecurrenceRuleTests</c>, where the clock can be pinned
/// to March; here the sweep runs on the real clock, so this test pins the occurrence into the current
/// grace window instead.</para>
/// </summary>
[Collection("Integration")]
public sealed class RecurringReminderSweepTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly string _conn;
    private readonly Guid _userId = Guid.NewGuid();
    private const string ChatId = "555000222";

    public RecurringReminderSweepTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _conn = factory.ConnectionString;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_recurring_occurrence_fires_once_and_a_second_sweep_over_the_same_tick_does_not_duplicate()
    {
        await LinkTelegramAsync(_userId, ChatId);
        // A daily reminder whose most recent occurrence sits five minutes ago — inside the grace window.
        var reminderId = await InsertRecurringReminderAsync(_userId, "Tomar remédio", "FREQ=DAILY");

        var first = await SweepAsync();
        Assert.True(first.IsSuccess);
        Assert.True(first.Value >= 1);

        // One occurrence, one ledger row; the reminder itself stays Scheduled (the series lives on).
        Assert.Equal(1, await DispatchCountAsync(reminderId));
        Assert.Equal("Scheduled", await ReminderStatusAsync(reminderId));

        // The notification carried the occurrence-aware buttons.
        var (template, rendered) = await LatestTelegramNotificationAsync(ChatId);
        Assert.Equal("agenda.reminder.due", template);
        Assert.False(string.IsNullOrWhiteSpace(rendered));

        // Re-running the sweep over the same tick is idempotent: no second dispatch row.
        var second = await SweepAsync();
        Assert.True(second.IsSuccess);
        Assert.Equal(1, await DispatchCountAsync(reminderId));
    }

    [Fact]
    public async Task Acknowledging_an_occurrence_leaves_the_series_running()
    {
        await LinkTelegramAsync(_userId, ChatId);
        var reminderId = await InsertRecurringReminderAsync(_userId, "Alongar", "FREQ=DAILY");

        await SweepAsync();
        var occurrence = await OccurrenceOfAsync(reminderId);

        await PublishInteractionAsync("task_done", $"{reminderId}|{occurrence.UtcTicks}");

        // The occurrence is acknowledged; the reminder stays Scheduled so tomorrow still fires.
        Assert.True(await OccurrenceAcknowledgedAsync(reminderId));
        Assert.Equal("Scheduled", await ReminderStatusAsync(reminderId));
    }

    // ── helpers ──

    private async Task<Result<int>> SweepAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new DispatchDueRemindersCommand(new DispatchDueRemindersInput(BatchSize: 50)), CancellationToken.None);
    }

    private async Task PublishInteractionAsync(string action, string payload)
    {
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IIntegrationEventBus>();
        await bus.PublishAsync(new InboundInteractionReceived(
            Guid.NewGuid(), DateTimeOffset.UtcNow, _userId, "telegram", "agenda", action, payload),
            CancellationToken.None);
    }

    private async Task LinkTelegramAsync(Guid userId, string chatId)
    {
        await ExecuteAsync("""
            INSERT INTO channels.chn001_user_channel
                (user_id, channel, address, locale, is_verified, verified_at, is_enabled, metadata)
            VALUES ($1, 'telegram', $2, 'pt-BR', true, current_timestamp, true, '{}'::jsonb)
            """, cmd =>
        {
            cmd.Parameters.AddWithValue(userId);
            cmd.Parameters.AddWithValue(chatId);
        });
    }

    private async Task<Guid> InsertRecurringReminderAsync(Guid userId, string title, string rrule)
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync("""
            INSERT INTO agenda.agd006_reminder
                (id, user_id, title, remind_at, time_zone, rrule, status)
            VALUES ($1, $2, $3, current_timestamp - interval '5 minutes', 'UTC', $4, 'Scheduled')
            """, cmd =>
        {
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(userId);
            cmd.Parameters.AddWithValue(title);
            cmd.Parameters.AddWithValue(rrule);
        });
        return id;
    }

    private async Task<int> DispatchCountAsync(Guid reminderId) =>
        Convert.ToInt32(await ScalarAsync("SELECT count(*) FROM agenda.agd006x_reminder_dispatch WHERE reminder_id = $1",
            cmd => cmd.Parameters.AddWithValue(reminderId)));

    private async Task<DateTimeOffset> OccurrenceOfAsync(Guid reminderId)
    {
        // Npgsql maps timestamptz to a UTC DateTime; normalize to the offset the button encodes.
        var value = await ScalarAsync(
            "SELECT occurrence_starts_at FROM agenda.agd006x_reminder_dispatch WHERE reminder_id = $1",
            cmd => cmd.Parameters.AddWithValue(reminderId));
        var utc = DateTime.SpecifyKind((DateTime)value!, DateTimeKind.Utc);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private async Task<bool> OccurrenceAcknowledgedAsync(Guid reminderId) =>
        (await ScalarAsync("SELECT acknowledged_at FROM agenda.agd006x_reminder_dispatch WHERE reminder_id = $1",
            cmd => cmd.Parameters.AddWithValue(reminderId))) is not null and not DBNull;

    private async Task<string> ReminderStatusAsync(Guid reminderId) =>
        (string)(await ScalarAsync("SELECT status FROM agenda.agd006_reminder WHERE id = $1",
            cmd => cmd.Parameters.AddWithValue(reminderId)))!;

    private async Task<(string Template, string? RenderedPayload)> LatestTelegramNotificationAsync(string chatId)
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT template_key, rendered_payload::text
            FROM channels.chn006_notification
            WHERE channel = 'telegram' AND recipient = $1
            ORDER BY created_at DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue(chatId);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "no telegram notification was queued");
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
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
