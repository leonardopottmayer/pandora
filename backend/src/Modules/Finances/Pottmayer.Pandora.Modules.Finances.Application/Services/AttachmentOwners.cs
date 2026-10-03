using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>
/// What upload, assign and delete share about an attachment's owner — a transaction, a pending transaction
/// or a card statement: that it is the user's, and the entry its audit trail gets. A queued attachment has
/// no owner and no trail.
/// </summary>
internal static class AttachmentOwners
{
    /// <summary>Whether the one given owner exists and belongs to the user.</summary>
    public static async Task<bool> ExistsAsync(
        IDataContext ctx, Guid userId, Guid? transactionId, Guid? pendingTransactionId, Guid? cardStatementId, CancellationToken ct)
    {
        if (transactionId is { } txId)
            return await ctx.AcquireRepository<ITransactionRepository>().FindByIdForUserAsync(txId, userId, ct) is not null;
        if (pendingTransactionId is { } pendingId)
            return await ctx.AcquireRepository<IPendingTransactionRepository>().FindByIdForUserAsync(pendingId, userId, ct) is not null;
        return await ctx.AcquireRepository<ICardStatementRepository>().FindByIdForUserAsync(cardStatementId!.Value, userId, ct) is not null;
    }

    /// <summary>Records the file arriving at (or leaving) its owner; nothing for a queued one.</summary>
    public static Task RecordAsync(IDataContext ctx, Attachment attachment, bool added, DateTimeOffset at, CancellationToken ct)
    {
        (string EntityType, Guid OwnerId, string Added, string Removed)? owner = attachment switch
        {
            { TransactionId: { } id } => (TransactionEvents.EntityType, id, TransactionEvents.AttachmentAdded, TransactionEvents.AttachmentRemoved),
            { PendingTransactionId: { } id } => (PendingTransactionEvents.EntityType, id, PendingTransactionEvents.AttachmentAdded, PendingTransactionEvents.AttachmentRemoved),
            { CardStatementId: { } id } => (StatementEvents.EntityType, id, StatementEvents.AttachmentAdded, StatementEvents.AttachmentRemoved),
            _ => null
        };
        if (owner is not { } o)
            return Task.CompletedTask;

        return ctx.RecordAsync(attachment.UserId, attachment.UserId, o.EntityType, o.OwnerId, added ? o.Added : o.Removed, at,
            new { attachmentId = attachment.Id, kind = attachment.Kind.Value, fileName = attachment.FileName }, ct: ct);
    }
}
