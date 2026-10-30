using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Contracts;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;

/// <summary>
/// Provider calls happen outside any transaction; each result is applied in its own unit of work, so a
/// slow server never holds a database transaction open and a failure keeps what was already applied.
/// </summary>
/// <remarks>
/// Serialized per mailbox by an in-process gate (the same single-instance assumption as
/// <c>ExternalCredentialProvider</c>), so the job and "Sync now" never apply the same cursor twice.
/// </remarks>
public sealed class SyncMailboxCommandHandler(
    IUnitOfWorkFactory factory,
    IServiceProvider services,
    IIntegrationEventBus bus,
    IOptions<CommunicationsOptions> options,
    TimeProvider timeProvider,
    ILogger<SyncMailboxCommandHandler> logger) : CommandHandlerBase<SyncMailboxCommand, bool>
{
    private const string NotifyCategory = "communications.mailbox";
    private const string AuthFailedTemplate = "communications.mailbox-auth-failed";

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    protected override async Task<Result<bool>> HandleAsync(SyncMailboxCommand request, CancellationToken ct)
    {
        var mailboxId = request.Input.MailboxId;
        var gate = Gates.GetOrAdd(mailboxId, _ => new SemaphoreSlim(1, 1));
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
        var labels = await provider.ListLabelsAsync(mailbox, ct);
        if (!labels.IsSuccess)
            return Result<bool>.Failure(labels.Errors);
        await ExecuteAsync((context, token) => ApplyLabelsAsync(context, mailbox.Id, labels.Value, token), ct);

        if (mailbox.SyncCursor is not { } syncCursor)
        {
            var started = await StartBackfillAsync(mailbox, provider, wipe: false, ct);
            if (!started.IsSuccess)
                return started;
        }
        else
        {
            var changes = await provider.FetchChangesAsync(mailbox, syncCursor, ct);
            if (!changes.IsSuccess && changes.Errors[0].Code == MailErrors.SyncCursorExpired.Code)
            {
                // The provider no longer knows where we were: read the mailbox again from the start.
                var restarted = await StartBackfillAsync(mailbox, provider, wipe: true, ct);
                if (!restarted.IsSuccess)
                    return restarted;
            }
            else if (!changes.IsSuccess)
            {
                return Result<bool>.Failure(changes.Errors);
            }
            else
            {
                var value = changes.Value;
                await UpdateMailboxAsync(mailbox.Id, async (context, m) =>
                {
                    await ApplyMessagesAsync(context, m, value.Upserts, value.RemovedIds, value.Snapshots, publish: true, ct);
                    m.SetSyncCursor(value.SyncCursor);
                }, ct);
            }
        }

        // Batches back to back until the backfill ends or the budget runs out; each is saved on its own.
        var budgetEnds = timeProvider.GetUtcNow() + options.Value.BackfillBudget;
        mailbox = (await LoadMailboxAsync(mailbox.Id, ct))!;
        while (!mailbox.IsBackfillComplete && mailbox.BackfillCursor is { } backfillCursor)
        {
            var batch = await provider.FetchBackfillBatchAsync(mailbox, backfillCursor, options.Value.BackfillBatchSize, ct);
            if (!batch.IsSuccess)
                return Result<bool>.Failure(batch.Errors);

            var advanced = await UpdateMailboxAsync(mailbox.Id, async (context, m) =>
            {
                await ApplyMessagesAsync(context, m, batch.Value.Messages, [], [], publish: false, ct);
                m.AdvanceBackfill(batch.Value.NextCursor, timeProvider.GetUtcNow());
            }, ct);
            if (advanced is null)
                return Result<bool>.Success(false);
            mailbox = advanced;

            if (timeProvider.GetUtcNow() >= budgetEnds)
                break;
        }

        await UpdateMailboxAsync(mailbox.Id, (_, m) =>
        {
            m.RecordSuccess(timeProvider.GetUtcNow());
            return Task.CompletedTask;
        }, ct);
        return Result<bool>.Success(true);
    }

    private async Task<Result<bool>> StartBackfillAsync(Mailbox mailbox, IMailboxProvider provider, bool wipe, CancellationToken ct)
    {
        var start = await provider.StartBackfillAsync(mailbox, ct);
        if (!start.IsSuccess)
            return Result<bool>.Failure(start.Errors);

        await UpdateMailboxAsync(mailbox.Id, async (context, m) =>
        {
            if (wipe)
                await context.AcquireRepository<IMailMessageRepository>().DeleteByMailboxAsync(m.Id, ct);
            m.StartBackfill(start.Value.SyncCursor, start.Value.BackfillCursor, start.Value.TotalEstimate, timeProvider.GetUtcNow());
        }, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// A label the provider no longer lists is deleted. On IMAP a label is the folder the message lives in,
    /// so its messages go with it; on Gmail they only lose the label.
    /// </summary>
    private static async Task ApplyLabelsAsync(
        IDataContext context, Guid mailboxId, IReadOnlyList<RemoteLabel> remote, CancellationToken ct)
    {
        var labels = context.AcquireRepository<IMailLabelRepository>();
        var messages = context.AcquireRepository<IMailMessageRepository>();
        var local = (await labels.ListByMailboxAsync(mailboxId, ct)).ToDictionary(l => l.ProviderLabelId);

        foreach (var r in remote)
        {
            if (local.Remove(r.ProviderLabelId, out var label))
            {
                label.Update(r.Name, r.Kind, r.Role, r.IsExclusive);
                await labels.UpdateAsync(label, ct);
            }
            else
            {
                await labels.AddAsync(MailLabel.Create(mailboxId, r.ProviderLabelId, r.Name, r.Kind, r.Role, r.IsExclusive), ct);
            }
        }

        foreach (var gone in local.Values)
        {
            if (gone.IsExclusive)
            {
                var ids = (await messages.ListKeysUnderLabelAsync(gone.Id, ct)).Select(k => k.ProviderMessageId).ToList();
                await messages.DeleteByProviderIdsAsync(mailboxId, ids, ct);
            }
            await labels.RemoveAsync(gone, ct);
        }
    }

    private async Task ApplyMessagesAsync(
        IDataContext context, Mailbox mailbox,
        IReadOnlyList<RemoteMessage> upserts, IReadOnlyList<string> removedIds, IReadOnlyList<RemoteLabelSnapshot> snapshots,
        bool publish, CancellationToken ct)
    {
        var messages = context.AcquireRepository<IMailMessageRepository>();
        var labelIds = (await context.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailbox.Id, ct))
            .ToDictionary(l => l.ProviderLabelId, l => l.Id);

        if (removedIds.Count > 0)
            await messages.DeleteByProviderIdsAsync(mailbox.Id, removedIds, ct);

        // A message missing from its label's snapshot loses the label; with none left, it is gone.
        foreach (var snapshot in snapshots)
        {
            if (!labelIds.TryGetValue(snapshot.ProviderLabelId, out var labelId))
                continue;

            var present = snapshot.ProviderMessageIds.ToHashSet();
            var missing = (await messages.ListKeysUnderLabelAsync(labelId, ct))
                .Where(k => !present.Contains(k.ProviderMessageId))
                .Select(k => k.Id)
                .ToList();

            var gone = new List<string>();
            foreach (var message in missing.Count > 0 ? await messages.ListByIdsAsync(missing, ct) : [])
            {
                if (message.Labels.All(l => l.LabelId == labelId))
                {
                    gone.Add(message.ProviderMessageId);
                    continue;
                }

                message.RemoveLabel(labelId);
                await messages.UpdateAsync(message, ct);
            }

            // Deleted in bulk: EF cannot delete a tracked entity whose JSON collections it tracks.
            if (gone.Count > 0)
                await messages.DeleteByProviderIdsAsync(mailbox.Id, gone, ct);
        }

        if (upserts.Count == 0)
            return;

        var existing = (await messages.ListByProviderIdsAsync(mailbox.Id, upserts.Select(u => u.ProviderMessageId).ToList(), ct))
            .ToDictionary(m => m.ProviderMessageId);
        var seenRfc822 = new HashSet<string>();

        foreach (var remote in upserts)
        {
            var labels = remote.ProviderLabelIds.Where(labelIds.ContainsKey).Select(id => labelIds[id]).ToList();

            if (existing.TryGetValue(remote.ProviderMessageId, out var message))
            {
                message.ApplyRemote(remote, labels);
                await messages.UpdateAsync(message, ct);
                continue;
            }

            // An arrival is a message the mailbox has never had: a move or a copy keeps its Message-ID.
            var isArrival = publish && (remote.Rfc822MessageId is not { } rfc822
                                        || (seenRfc822.Add(rfc822) && !await messages.ExistsWithRfc822IdAsync(mailbox.Id, rfc822, ct)));

            var created = MailMessage.FromRemote(mailbox.Id, remote, labels);
            await messages.AddAsync(created, ct);

            if (isArrival)
                await bus.PublishAsync(new MailMessageReceived(
                    EventId: Guid.CreateVersion7(),
                    OccurredAt: timeProvider.GetUtcNow(),
                    UserId: mailbox.UserId,
                    MailboxId: mailbox.Id,
                    MessageId: created.Id,
                    From: remote.From?.Address,
                    Subject: remote.Subject,
                    Labels: remote.ProviderLabelIds), ct);
        }
    }

    /// <summary>
    /// A refused credential stops the sync until the user reconnects. An IMAP password is told about once,
    /// on the change; a Google account is not, since Integrations already warns when its grant is revoked.
    /// </summary>
    private Task RecordFailureAsync(Guid mailboxId, Error error, CancellationToken ct) =>
        UpdateMailboxAsync(mailboxId, async (_, m) =>
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

    /// <summary>Loads the mailbox in a unit of work, lets <paramref name="change"/> act, saves it; null when it is gone.</summary>
    private async Task<Mailbox?> UpdateMailboxAsync(
        Guid mailboxId, Func<IDataContext, Mailbox, Task> change, CancellationToken ct) =>
        await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (context, token) =>
        {
            var repository = context.AcquireRepository<IMailboxRepository>();
            var mailbox = await repository.GetByIdAsync(mailboxId, token);
            if (mailbox is null)
                return null;

            await change(context, mailbox);
            await repository.UpdateAsync(mailbox, token);
            return mailbox;
        }, cancellationToken: ct);

    private Task ExecuteAsync(Func<IDataContext, CancellationToken, Task> work, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, work, cancellationToken: ct);
}
