using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Sweep;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Infrastructure.Jobs;

/// <summary>
/// Periodically fires due reminders. The module-owned job pattern the rest of Pandora uses: a
/// <see cref="PeriodicTimer"/> driving a CQRS command in a fresh scope, with durability coming from
/// the reminder's own status rather than a queue.
/// </summary>
public sealed class ReminderSweepBackgroundService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<AgendaOptions> options,
    ILogger<ReminderSweepBackgroundService> logger) : BackgroundService
{
    private readonly AgendaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.SweepIntervalSeconds));
        using var timer = new PeriodicTimer(interval, timeProvider);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reminder sweep failed.");
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

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var fired = await sender.Send(
            new DispatchDueRemindersCommand(new DispatchDueRemindersInput(_options.SweepBatchSize)), ct)
            .ConfigureAwait(false);

        if (fired.IsSuccess && fired.Value > 0)
            logger.LogInformation("Fired {Count} due reminder(s).", fired.Value);
    }
}
