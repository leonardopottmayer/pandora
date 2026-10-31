using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;

/// <summary>
/// One cycle: labels and changes (<see cref="MailboxChangesStep"/>), then backfill batches while there is budget.
/// Provider calls happen outside any transaction; each result is applied in its own unit of work, so a slow server
/// never holds a database transaction open and a failure keeps what was already applied.
/// </summary>
/// <remarks>
/// The whole cycle holds <see cref="MailboxGates.Cycle"/>, so the job and "Sync now" never apply the same cursor
/// twice. The backfill runs outside <see cref="MailboxGates.Action"/>: a stale batch can overwrite a user's action,
/// and the next cycle corrects it, since the action is a change at the provider.
/// </remarks>
public sealed class SyncMailboxCommandHandler(
    IUnitOfWorkFactory factory,
    IServiceProvider services,
    IIntegrationEventBus bus,
    MailboxGates gates,
    MailboxChangesStep changes,
    IOptions<CommunicationsOptions> options,
    TimeProvider timeProvider,
    ILogger<SyncMailboxCommandHandler> logger) : CommandHandlerBase<SyncMailboxCommand, bool>
{
    private const string NotifyCategory = "communications.mailbox";
    private const string AuthFailedTemplate = "communications.mailbox-auth-failed";

    protected override async Task<Result<bool>> HandleAsync(SyncMailboxCommand request, CancellationToken ct)
    {
        var mailboxId = request.Input.MailboxId;
        var gate = gates.Cycle(mailboxId);
        await gate.WaitAsync(ct);
        try
        {
            var mailbox = await LoadMailboxAsync(mailboxId, ct);
            if (mailbox is null || !mailbox.CanSync)
                return Result<bool>.Success(false);

            var provider = services.GetRequiredKeyedService<IMailboxProvider>(mailbox.Provider.Value);
            Result<bool> cycle;
            try
            {
                cycle = await RunCycleAsync(mailbox, provider, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // A bug or a failed write, not a provider's answer: the mailbox must not look healthy while it
                // fails the same way every cycle. The details stay in the log, the screen gets the fixed text.
                logger.LogError(ex, "Mailbox {MailboxId} sync cycle threw.", mailboxId);
                cycle = Result<bool>.Failure(MailErrors.SyncFailed);
            }

            if (cycle.IsSuccess)
                return cycle;

            await RecordFailureAsync(mailboxId, cycle.Errors[0], ct);
            return cycle;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Result<bool>> RunCycleAsync(Mailbox mailbox, IMailboxProvider provider, CancellationToken ct)
    {
        var upToDate = await changes.RunAsync(mailbox.Id, provider, ct);
        if (!upToDate.IsSuccess || !upToDate.Value)
            return upToDate;

        // Batches back to back until the backfill ends or the budget runs out; each is saved on its own.
        var budgetEnds = timeProvider.GetUtcNow() + options.Value.BackfillBudget;
        mailbox = (await LoadMailboxAsync(mailbox.Id, ct))!;
        while (!mailbox.IsBackfillComplete && mailbox.BackfillCursor is { } backfillCursor)
        {
            var batch = await provider.FetchBackfillBatchAsync(mailbox, backfillCursor, options.Value.BackfillBatchSize, ct);
            if (!batch.IsSuccess)
                return Result<bool>.Failure(batch.Errors);

            var advanced = await factory.UpdateMailboxAsync(mailbox.Id, async (context, m) =>
            {
                await changes.ApplyBackfillBatchAsync(context, m, batch.Value.Messages, ct);
                m.AdvanceBackfill(batch.Value.NextCursor, timeProvider.GetUtcNow());
            }, ct);
            if (advanced is null)
                return Result<bool>.Success(false);
            mailbox = advanced;

            if (timeProvider.GetUtcNow() >= budgetEnds)
                break;
        }

        await factory.UpdateMailboxAsync(mailbox.Id, (_, m) =>
        {
            m.RecordSuccess(timeProvider.GetUtcNow());
            return Task.CompletedTask;
        }, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// A refused credential stops the sync until the user reconnects. An IMAP password is told about once,
    /// on the change; a Google account is not, since Integrations already warns when its grant is revoked.
    /// </summary>
    private Task RecordFailureAsync(Guid mailboxId, Error error, CancellationToken ct) =>
        factory.UpdateMailboxAsync(mailboxId, async (_, m) =>
        {
            var needsUser = error.Code == MailErrors.MissingScopes.Code || error.Code == MailErrors.MailboxAuthFailed(string.Empty).Code;
            if (!needsUser)
            {
                m.RecordFailure(error.Message);
                return;
            }

            var alreadyStopped = m.Status == MailboxStatus.NeedsReconnect;
            m.MarkNeedsReconnect(error.Message);
            if (alreadyStopped || m.Provider == MailProvider.Gmail)
                return;

            await bus.PublishAsync(new NotifyUserRequested(
                EventId: Guid.CreateVersion7(),
                OccurredAt: timeProvider.GetUtcNow(),
                UserId: m.UserId,
                Category: NotifyCategory,
                TemplateKey: AuthFailedTemplate,
                Locale: null,
                Channels: null,
                Payload: new Dictionary<string, string> { ["mailbox"] = m.Address },
                CorrelationId: Guid.CreateVersion7()), ct);
        }, ct);

    private Task<Mailbox?> LoadMailboxAsync(Guid mailboxId, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<IMailboxRepository>().GetByIdAsync(mailboxId, token), cancellationToken: ct);
}
