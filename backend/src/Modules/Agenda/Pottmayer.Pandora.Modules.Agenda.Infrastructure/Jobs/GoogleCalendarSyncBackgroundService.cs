using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncCalendarBinding;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncTaskListBinding;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Infrastructure.Jobs;

/// <summary>
/// Every <see cref="AgendaOptions.GoogleSyncInterval"/>: one sync cycle per Google calendar binding, then per
/// Google task list binding, in <c>ok</c>. A binding that fails is recorded on itself and the walk goes on.
/// </summary>
/// <remarks>ponytail: bindings in sequence, like the mail sync; per-binding tasks if the walk stops fitting in the interval.</remarks>
public sealed class GoogleCalendarSyncBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<AgendaOptions> options,
    TimeProvider timeProvider,
    ILogger<GoogleCalendarSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.GoogleSyncInterval, timeProvider);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar sync job failed.");
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

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var bindingIds = await factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<ICalendarBindingRepository>().ListSyncableIdsAsync(token), cancellationToken: ct);

        foreach (var bindingId in bindingIds)
        {
            try
            {
                var result = await sender.Send(new SyncCalendarBindingCommand(new SyncCalendarBindingInput(bindingId)), ct);
                if (result.IsFailure)
                    logger.LogWarning("Calendar binding {BindingId} sync failed: {Errors}.", bindingId,
                        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Calendar binding {BindingId} sync threw.", bindingId);
            }
        }

        var taskBindingIds = await factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<ITaskListBindingRepository>().ListSyncableIdsAsync(token), cancellationToken: ct);

        foreach (var bindingId in taskBindingIds)
        {
            try
            {
                var result = await sender.Send(new SyncTaskListBindingCommand(new SyncTaskListBindingInput(bindingId)), ct);
                if (result.IsFailure)
                    logger.LogWarning("Task list binding {BindingId} sync failed: {Errors}.", bindingId,
                        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Task list binding {BindingId} sync threw.", bindingId);
            }
        }
    }
}
