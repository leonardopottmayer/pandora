using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.Jobs;

/// <summary>
/// Every <see cref="CommunicationsOptions.SyncInterval"/>: one sync cycle per mailbox, then the content
/// cache retention. A mailbox that fails is logged and the walk goes on.
/// </summary>
/// <remarks>
/// ponytail: mailboxes in sequence, so one slow server delays the others; per-mailbox tasks if the walk
/// stops fitting in the interval.
/// </remarks>
public sealed class MailboxSyncBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<CommunicationsOptions> options,
    TimeProvider timeProvider,
    ILogger<MailboxSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SyncInterval, timeProvider);

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
                logger.LogError(ex, "Mailbox sync job failed.");
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

        var mailboxIds = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<IMailboxRepository>().ListSyncableIdsAsync(token), cancellationToken: ct);

        foreach (var mailboxId in mailboxIds)
        {
            try
            {
                var result = await sender.Send(new SyncMailboxCommand(new SyncMailboxInput(mailboxId)), ct);
                if (result.IsFailure)
                    logger.LogWarning("Mailbox {MailboxId} sync failed: {Errors}.", mailboxId,
                        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Mailbox {MailboxId} sync threw.", mailboxId);
            }
        }

        var cutoff = timeProvider.GetUtcNow().AddDays(-options.Value.ContentCacheRetentionDays);
        var evicted = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<IMailContentRepository>().DeleteNotAccessedSinceAsync(cutoff, token), cancellationToken: ct);
        if (evicted > 0)
            logger.LogInformation("Evicted {Count} cached mail bodies not opened since {Cutoff:u}.", evicted, cutoff);
    }
}
