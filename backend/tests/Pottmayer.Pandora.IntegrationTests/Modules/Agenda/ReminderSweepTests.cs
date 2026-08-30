using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Sweep;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Messaging.Abstractions;
using Pottmayer.Tars.Messaging.Broker.Dispatch;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// The Reminders vertical slice against a real database: a due reminder is swept into a Telegram
/// notification with inline buttons, and a tapped button (an inbound interaction) acknowledges it —
/// the full loop, minus the actual Telegram round-trip.
/// </summary>
[Collection("Integration")]
public sealed class ReminderSweepTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly string _conn;
    private readonly Guid _userId = Guid.NewGuid();
    private const string ChatId = "555000111";

    public ReminderSweepTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _conn = factory.ConnectionString;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_due_reminder_is_swept_into_a_notification_with_buttons_and_a_tap_acknowledges_it()
    {
        await LinkTelegramAsync(_userId, ChatId);
        var reminderId = await InsertDueReminderAsync(_userId, "Pagar a conta de internet");

        // Sweep: the real handler publishes NotifyUserRequested, which the Channels fan-out turns into
        // a queued Telegram notification with registered interaction buttons.
        var fired = await SweepAsync();
        Assert.True(fired.IsSuccess);
        Assert.True(fired.Value >= 1);

        Assert.Equal("Notified", await ReminderStatusAsync(reminderId));

        var (template, renderedPayload) = await LatestTelegramNotificationAsync(ChatId);
        Assert.Equal("agenda.reminder.due", template);
        Assert.False(string.IsNullOrWhiteSpace(renderedPayload)); // carries the inline keyboard
        Assert.Equal(2, await InteractionCountAsync(_userId));      // task_done + snooze_1h

        // A tapped "Done" arrives as an inbound interaction; the Agenda subscriber acknowledges.
        await PublishInteractionAsync("task_done", reminderId);
        Assert.Equal("Acknowledged", await ReminderStatusAsync(reminderId));
    }

    [Fact]
    public async Task Snoozing_pushes_the_reminder_out_and_it_is_not_re_fired_immediately()
    {
        await LinkTelegramAsync(_userId, ChatId);
        var reminderId = await InsertDueReminderAsync(_userId, "Tomar remédio");

        await SweepAsync();
        await PublishInteractionAsync("snooze_1h", reminderId);

        Assert.Equal("Snoozed", await ReminderStatusAsync(reminderId));

        // A second sweep now finds nothing due: the snooze pushed it an hour out.
        var second = await SweepAsync();
        Assert.True(second.IsSuccess);
        Assert.Equal(0, second.Value);
    }

    // ── helpers ──

    private async Task<Result<int>> SweepAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new DispatchDueRemindersCommand(new DispatchDueRemindersInput(BatchSize: 50)), CancellationToken.None);
        await _factory.DrainOutboxAsync(); // deliver the NotifyUserRequested events the sweep parked in the outbox
        return result;
    }

    private async Task PublishInteractionAsync(string action, Guid reminderId)
    {
        // Simulate the relay delivering an inbound interaction: dispatch straight to the handlers,
        // exactly as the outbox relay would (a bare publish would need an open unit of work).
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        await dispatcher.DispatchAsync(new InboundInteractionReceived(
            Guid.NewGuid(), DateTimeOffset.UtcNow, _userId, "telegram", "agenda", action, reminderId.ToString()),
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

    private async Task<Guid> InsertDueReminderAsync(Guid userId, string title)
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync("""
            INSERT INTO agenda.agd006_reminder
                (id, user_id, title, remind_at, time_zone, status)
            VALUES ($1, $2, $3, current_timestamp - interval '1 minute', 'UTC', 'Scheduled')
            """, cmd =>
        {
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(userId);
            cmd.Parameters.AddWithValue(title);
        });
        return id;
    }

    private async Task<string> ReminderStatusAsync(Guid reminderId) =>
        (string)(await ScalarAsync("SELECT status FROM agenda.agd006_reminder WHERE id = $1",
            cmd => cmd.Parameters.AddWithValue(reminderId)))!;

    private async Task<int> InteractionCountAsync(Guid userId) =>
        Convert.ToInt32(await ScalarAsync("SELECT count(*) FROM channels.chn003_interaction WHERE user_id = $1",
            cmd => cmd.Parameters.AddWithValue(userId)));

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
