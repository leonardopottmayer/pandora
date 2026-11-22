using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateAlert;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Sweep;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Messaging.Abstractions;
using Pottmayer.Tars.Messaging.Broker.Dispatch;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// The task-alert slice against a real database. It proves the two Phase-3 acceptance criteria:
/// a weekly recurring task completed today reappears next week with its alerts (across a spring-forward),
/// and a task alert fires exactly once per anchor — re-running the sweep over the same tick writes no
/// duplicate (the UNIQUE (alert, occurrence) guard) — plus that an inline tap completes the task.
///
/// <para>The daylight-saving wall-clock guarantee is proven deterministically at the engine level in
/// <c>TaskTests</c> (pinned to March); the recurrence half here exercises it end to end through the real
/// completion path, and the idempotency half runs on the real clock, so it pins the anchor into the
/// current grace window instead.</para>
/// </summary>
[Collection("Integration")]
public sealed class TaskAlertSweepTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly string _conn;
    private readonly Guid _userId = Guid.NewGuid();
    private const string ChatId = "555000333";

    public TaskAlertSweepTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _conn = factory.ConnectionString;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_weekly_task_completed_today_reappears_next_week_with_its_alerts_across_dst()
    {
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        // 2026-03-01 09:00 New York is a Sunday, one week before the 2026-03-08 spring-forward.
        var due = new DateTimeOffset(new DateTime(2026, 3, 1, 9, 0, 0), ny.GetUtcOffset(new DateTime(2026, 3, 1, 9, 0, 0)));

        var listId = (await SendAsync(new CreateTaskListCommand(new CreateTaskListInput(_userId, "Casa", true, 0)))).Value.Id;
        var task = (await SendAsync(new CreateTaskCommand(new CreateTaskInput(
            _userId, listId, null, "Trocar lençóis", "toda semana", due, DueHasTime: true,
            TaskPriority.Low, "America/New_York", "FREQ=WEEKLY;BYDAY=SU")))).Value;
        await SendAsync(new CreateAlertCommand(new CreateAlertInput(_userId, "task", task.Id, OffsetMinutes: -60, Channels: null)));

        var completed = await SendAsync(new CompleteTaskCommand(new CompleteTaskInput(_userId, task.Id)));
        Assert.True(completed.IsSuccess);

        // The original instance is closed.
        Assert.Equal("Done", await TaskStatusAsync(task.Id));

        // The next instance exists, one week on, at the same 09:00 wall clock — so its UTC instant moved
        // by the DST hour (09:00 EDT = 13:00Z, not the 14:00Z that 09:00 EST was).
        var (spawnedId, spawnedDue) = await SpawnedTodoTaskAsync(task.Id);
        var expectedNext = new DateTimeOffset(new DateTime(2026, 3, 8, 9, 0, 0), ny.GetUtcOffset(new DateTime(2026, 3, 8, 9, 0, 0)))
            .ToUniversalTime();
        Assert.Equal(expectedNext, spawnedDue);

        // Its alert was carried over.
        Assert.Equal(1, await AlertCountForSubjectAsync(spawnedId));
    }

    [Fact]
    public async Task A_task_alert_fires_once_and_a_second_sweep_over_the_same_tick_does_not_duplicate()
    {
        await LinkTelegramAsync(_userId, ChatId);

        var listId = (await SendAsync(new CreateTaskListCommand(new CreateTaskListInput(_userId, "Casa", true, 0)))).Value.Id;
        // Due five minutes ago with a zero offset: the firing anchor sits inside the grace window.
        var due = DateTimeOffset.UtcNow.AddMinutes(-5);
        var task = (await SendAsync(new CreateTaskCommand(new CreateTaskInput(
            _userId, listId, null, "Regar plantas", null, due, DueHasTime: true,
            TaskPriority.None, "UTC", Rrule: null)))).Value;
        await SendAsync(new CreateAlertCommand(new CreateAlertInput(_userId, "task", task.Id, OffsetMinutes: 0, Channels: null)));

        var first = await SweepAsync();
        Assert.True(first.IsSuccess);
        Assert.True(first.Value >= 1);
        Assert.Equal(1, await DispatchCountAsync());

        // The notification went out on the task template.
        var (template, rendered) = await LatestTelegramNotificationAsync(ChatId);
        Assert.Equal("agenda.task.due", template);
        Assert.False(string.IsNullOrWhiteSpace(rendered));

        // Re-running the sweep over the same tick is idempotent: no second dispatch row.
        var second = await SweepAsync();
        Assert.True(second.IsSuccess);
        Assert.Equal(1, await DispatchCountAsync());
    }

    [Fact]
    public async Task An_inline_tap_completes_the_task()
    {
        var listId = (await SendAsync(new CreateTaskListCommand(new CreateTaskListInput(_userId, "Casa", true, 0)))).Value.Id;
        var due = DateTimeOffset.UtcNow.AddMinutes(-5);
        var task = (await SendAsync(new CreateTaskCommand(new CreateTaskInput(
            _userId, listId, null, "Levar o lixo", null, due, DueHasTime: true,
            TaskPriority.None, "UTC", Rrule: null)))).Value;

        await PublishInteractionAsync("task_done", $"task|{task.Id}");

        Assert.Equal("Done", await TaskStatusAsync(task.Id));
    }

    // ── helpers ──

    private async Task<Result<TResult>> SendAsync<TResult>(
        Pottmayer.Tars.Core.Cqrs.Commands.ICommand<TResult> command)
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command, CancellationToken.None);
    }

    private async Task<Result<int>> SweepAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new DispatchDueTaskAlertsCommand(new DispatchDueTaskAlertsInput(BatchSize: 50)), CancellationToken.None);
        await _factory.DrainOutboxAsync(); // deliver the NotifyUserRequested events the sweep parked in the outbox
        return result;
    }

    private async Task PublishInteractionAsync(string action, string payload)
    {
        // Simulate the relay delivering an inbound interaction: dispatch straight to the handlers,
        // exactly as the outbox relay would (a bare publish would need an open unit of work).
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        await dispatcher.DispatchAsync(new InboundInteractionReceived(
            Guid.NewGuid(), DateTimeOffset.UtcNow, _userId, "telegram", "agenda", action, payload),
            CancellationToken.None);
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

    private async Task<string> TaskStatusAsync(Guid taskId) =>
        (string)(await ScalarAsync("SELECT status FROM agenda.agd005_task WHERE id = $1",
            cmd => cmd.Parameters.AddWithValue(taskId)))!;

    private async Task<(Guid Id, DateTimeOffset DueAt)> SpawnedTodoTaskAsync(Guid originalId)
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, due_at FROM agenda.agd005_task WHERE user_id = $1 AND id <> $2 AND status = 'Todo'";
        cmd.Parameters.AddWithValue(_userId);
        cmd.Parameters.AddWithValue(originalId);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "no spawned task was found");
        var id = reader.GetGuid(0);
        var utc = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);
        Assert.False(await reader.ReadAsync(), "more than one open task was spawned");
        return (id, new DateTimeOffset(utc, TimeSpan.Zero));
    }

    private async Task<int> AlertCountForSubjectAsync(Guid subjectId) =>
        Convert.ToInt32(await ScalarAsync("SELECT count(*) FROM agenda.agd007_alert WHERE subject_id = $1",
            cmd => cmd.Parameters.AddWithValue(subjectId)));

    private async Task<int> DispatchCountAsync() =>
        Convert.ToInt32(await ScalarAsync("SELECT count(*) FROM agenda.agd008_alert_dispatch WHERE user_id = $1",
            cmd => cmd.Parameters.AddWithValue(_userId)));

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
