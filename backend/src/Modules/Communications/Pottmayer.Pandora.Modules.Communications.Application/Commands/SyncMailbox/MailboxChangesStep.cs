using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Contracts;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;

/// <summary>
/// The part of a cycle that brings the mirror up to date with the provider: labels, then changes. It runs under
/// the action gate, so it cannot interleave with a user's action. A mailbox with no cursor yet starts its backfill.
/// </summary>
/// <remarks>
/// A vanished label is removed only after the changes are applied: its messages may have moved (a renamed folder
/// looks like one gone and one new), and each must find its row before anything is deleted.
/// </remarks>
public sealed class MailboxChangesStep(
    IUnitOfWorkFactory factory,
    IIntegrationEventBus bus,
    MailboxGates gates,
    TimeProvider timeProvider)
{
    /// <summary>False when the mailbox no longer exists.</summary>
    public async Task<Result<bool>> RunAsync(Guid mailboxId, IMailboxProvider provider, CancellationToken ct)
    {
        var gate = gates.Action(mailboxId);
        await gate.WaitAsync(ct);
        try
        {
            var mailbox = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (context, token) =>
                context.AcquireRepository<IMailboxRepository>().GetByIdAsync(mailboxId, token), cancellationToken: ct);
            if (mailbox is null)
                return Result<bool>.Success(false);

            var labels = await provider.ListLabelsAsync(mailbox, ct);
            if (!labels.IsSuccess)
                return Result<bool>.Failure(labels.Errors);

            var vanished = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (context, token) =>
                AddAndUpdateLabelsAsync(context, mailbox.Id, labels.Value, token), cancellationToken: ct);

            var applied = await ApplyChangesAsync(mailbox, provider, ct);
            if (!applied.IsSuccess)
                return applied;

            if (vanished.Count > 0)
                await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (context, token) =>
                    RemoveLabelsAsync(context, mailbox.Id, vanished, token), cancellationToken: ct);

            return Result<bool>.Success(true);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>A batch of the backfill: no removals, and old mail is never an arrival.</summary>
    public Task ApplyBackfillBatchAsync(IDataContext context, Mailbox mailbox, IReadOnlyList<RemoteMessage> messages, CancellationToken ct) =>
        ApplyMessagesAsync(context, mailbox, messages, [], [], publish: false, ct);

    private async Task<Result<bool>> ApplyChangesAsync(Mailbox mailbox, IMailboxProvider provider, CancellationToken ct)
    {
        if (mailbox.SyncCursor is not { } syncCursor)
            return await StartBackfillAsync(mailbox, provider, wipe: false, ct);

        var changes = await provider.FetchChangesAsync(mailbox, syncCursor, ct);
        if (!changes.IsSuccess && changes.Errors[0].Code == MailErrors.SyncCursorExpired.Code)
        {
            // The provider no longer knows where we were: read the mailbox again from the start.
            return await StartBackfillAsync(mailbox, provider, wipe: true, ct);
        }

        if (!changes.IsSuccess)
            return Result<bool>.Failure(changes.Errors);

        var value = changes.Value;
        await factory.UpdateMailboxAsync(mailbox.Id, async (context, m) =>
        {
            await ApplyMessagesAsync(context, m, value.Upserts, value.RemovedIds, value.Snapshots, publish: true, ct);
            m.SetSyncCursor(value.SyncCursor);
        }, ct);
        return Result<bool>.Success(true);
    }

    private async Task<Result<bool>> StartBackfillAsync(Mailbox mailbox, IMailboxProvider provider, bool wipe, CancellationToken ct)
    {
        var start = await provider.StartBackfillAsync(mailbox, ct);
        if (!start.IsSuccess)
            return Result<bool>.Failure(start.Errors);

        await factory.UpdateMailboxAsync(mailbox.Id, async (context, m) =>
        {
            if (wipe)
                await context.AcquireRepository<IMailMessageRepository>().DeleteByMailboxAsync(m.Id, ct);
            m.StartBackfill(start.Value.SyncCursor, start.Value.BackfillCursor, start.Value.TotalEstimate, timeProvider.GetUtcNow());
        }, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>Returns the provider ids of the local labels the provider no longer lists.</summary>
    private static async Task<IReadOnlyCollection<string>> AddAndUpdateLabelsAsync(
        IDataContext context, Guid mailboxId, IReadOnlyList<RemoteLabel> remote, CancellationToken ct)
    {
        var labels = context.AcquireRepository<IMailLabelRepository>();
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

        return local.Keys;
    }

    /// <summary>
    /// On IMAP a label is the folder the message lives in, so what is still under it goes with it; on Gmail the
    /// messages only lose the label.
    /// </summary>
    private static async Task RemoveLabelsAsync(
        IDataContext context, Guid mailboxId, IReadOnlyCollection<string> providerLabelIds, CancellationToken ct)
    {
        var labels = context.AcquireRepository<IMailLabelRepository>();
        var messages = context.AcquireRepository<IMailMessageRepository>();

        foreach (var gone in (await labels.ListByMailboxAsync(mailboxId, ct)).Where(l => providerLabelIds.Contains(l.ProviderLabelId)))
        {
            if (gone.IsExclusive)
            {
                var ids = (await messages.ListKeysUnderLabelAsync(gone.Id, ct)).Select(k => k.ProviderMessageId).ToList();
                await messages.DeleteByProviderIdsAsync(mailboxId, ids, ct);
            }
            await labels.RemoveAsync(gone, ct);
        }
    }

    /// <summary>
    /// A message that vanished and reappears in the same apply with its Message-ID is the same message under a new
    /// provider id (an IMAP move): it keeps its row and its cached body, and it is not an arrival.
    /// </summary>
    /// <remarks>
    /// ponytail: only within one apply. A move from a folder the IMAP walk has not read yet to one it already read
    /// spans two cycles, so it is delete + create and can publish a false arrival; a window of seconds.
    /// </remarks>
    private async Task ApplyMessagesAsync(
        IDataContext context, Mailbox mailbox,
        IReadOnlyList<RemoteMessage> upserts, IReadOnlyList<string> removedIds, IReadOnlyList<RemoteLabelSnapshot> snapshots,
        bool publish, CancellationToken ct)
    {
        var messages = context.AcquireRepository<IMailMessageRepository>();
        var labelIds = (await context.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailbox.Id, ct))
            .ToDictionary(l => l.ProviderLabelId, l => l.Id);
        var upsertIds = upserts.Select(u => u.ProviderMessageId).ToHashSet();

        var vanishing = await FindVanishingAsync(messages, mailbox.Id, removedIds, snapshots, labelIds, ct);
        vanishing.RemoveWhere(m => upsertIds.Contains(m.ProviderMessageId));

        var existing = upserts.Count > 0
            ? (await messages.ListByProviderIdsAsync(mailbox.Id, upsertIds, ct)).ToDictionary(m => m.ProviderMessageId)
            : [];

        var moved = vanishing
            .Where(m => m.Rfc822MessageId is not null)
            .GroupBy(m => m.Rfc822MessageId!)
            .ToDictionary(g => g.Key, g => new Queue<MailMessage>(g));

        foreach (var remote in upserts)
        {
            if (!existing.ContainsKey(remote.ProviderMessageId)
                && remote.Rfc822MessageId is { } id
                && moved.TryGetValue(id, out var rows)
                && rows.TryDequeue(out var row))
            {
                vanishing.Remove(row);
                row.Rebind(remote.ProviderMessageId);
                existing[remote.ProviderMessageId] = row;
            }
        }

        // Deleted in bulk: EF cannot delete a tracked entity whose JSON collections it tracks.
        if (vanishing.Count > 0)
            await messages.DeleteByProviderIdsAsync(mailbox.Id, vanishing.Select(m => m.ProviderMessageId).ToList(), ct);

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

            // An arrival is a message the mailbox has never had: a copy keeps its Message-ID.
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
    /// The rows that leave the mailbox: the removed ids, and whatever a snapshot left without a label. A message
    /// missing from a snapshot that still has another label only loses that one.
    /// </summary>
    private static async Task<HashSet<MailMessage>> FindVanishingAsync(
        IMailMessageRepository messages, Guid mailboxId,
        IReadOnlyList<string> removedIds, IReadOnlyList<RemoteLabelSnapshot> snapshots,
        IReadOnlyDictionary<string, Guid> labelIds, CancellationToken ct)
    {
        var vanishing = new HashSet<MailMessage>();
        if (removedIds.Count > 0)
            vanishing.UnionWith(await messages.ListByProviderIdsAsync(mailboxId, removedIds, ct));

        foreach (var snapshot in snapshots)
        {
            if (!labelIds.TryGetValue(snapshot.ProviderLabelId, out var labelId))
                continue;

            var present = snapshot.ProviderMessageIds.ToHashSet();
            var missing = (await messages.ListKeysUnderLabelAsync(labelId, ct))
                .Where(k => !present.Contains(k.ProviderMessageId))
                .Select(k => k.Id)
                .ToList();

            foreach (var message in missing.Count > 0 ? await messages.ListByIdsAsync(missing, ct) : [])
            {
                if (message.Labels.All(l => l.LabelId == labelId))
                {
                    vanishing.Add(message);
                    continue;
                }

                message.RemoveLabel(labelId);
                await messages.UpdateAsync(message, ct);
            }
        }

        return vanishing;
    }
}
