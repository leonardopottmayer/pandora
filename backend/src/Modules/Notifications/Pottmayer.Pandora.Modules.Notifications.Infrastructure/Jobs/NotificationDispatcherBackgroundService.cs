using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Notifications.Application.Commands.DispatchPending;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>
/// Periodically drains the durable notification queue. Mirrors the
/// <c>RefreshTokenPurgeBackgroundService</c> pattern: a <see cref="PeriodicTimer"/> driving a CQRS
/// command in a fresh scope.
/// </summary>
public sealed class NotificationDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly NotificationsOptions _options;
    private readonly ILogger<NotificationDispatcherBackgroundService> _logger;

    public NotificationDispatcherBackgroundService(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        IOptions<NotificationsOptions> options,
        ILogger<NotificationDispatcherBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.DispatchIntervalSeconds));
        using var timer = new PeriodicTimer(interval, _timeProvider);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification dispatch failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var command = new DispatchPendingNotificationsCommand(
            new DispatchPendingNotificationsInput(_options.DispatchBatchSize));

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Notification dispatch command failed: {Errors}.",
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            return;
        }

        var outcome = result.Value;
        if (outcome is { Total: > 0 })
            _logger.LogInformation(
                "Dispatched notifications: {Sent} sent, {Failed} failed, {Dead} dead.",
                outcome.Sent, outcome.Failed, outcome.Dead);
    }
}
