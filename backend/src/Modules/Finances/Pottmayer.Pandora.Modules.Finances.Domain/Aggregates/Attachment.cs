using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// A file attached to a transaction, an inbox suggestion or a card statement — a boleto, a receipt, an
/// invoice (fin017). The bytes live in the module's <c>IFileStorage</c>; this row holds the metadata and
/// the <see cref="StorageKey"/> that locates them. It belongs to at most one of <see cref="TransactionId"/>,
/// <see cref="PendingTransactionId"/> or <see cref="CardStatementId"/>; with none it is
/// <see cref="IsQueued"/> — a file shared with the assistant bot, waiting for the user to file it
/// (<see cref="AssignTo"/>). A month's boleto is attached to the suggestion the recurrence produced, and
/// follows it onto the transaction when it is approved (<see cref="MoveToTransaction"/>), where the receipt
/// joins it. Write-once otherwise, so it carries just a <see cref="CreatedAt"/>.
/// </summary>
public sealed class Attachment : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? TransactionId { get; private set; }
    public Guid? PendingTransactionId { get; private set; }
    public Guid? CardStatementId { get; private set; }
    public AttachmentKind Kind { get; private set; } = AttachmentKind.Other;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    /// <summary>Which <c>IFileStorage</c> backend holds the bytes (e.g. <c>Database</c>, later <c>S3</c>).</summary>
    public string StorageBackend { get; private set; } = string.Empty;

    /// <summary>The opaque key that locates the bytes within <see cref="StorageBackend"/>.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    /// <summary>What the user wrote with the file when they shared it (the bot caption), if anything.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>No owner yet: in the queue, waiting to be filed.</summary>
    public bool IsQueued => TransactionId is null && PendingTransactionId is null && CardStatementId is null;

    private Attachment() { }

    /// <summary>
    /// Records a file whose bytes are already stored under <paramref name="storageKey"/>, on at most one
    /// owner — none puts it in the queue.
    /// </summary>
    public static Attachment Create(
        Guid userId,
        Guid? transactionId,
        Guid? pendingTransactionId,
        Guid? cardStatementId,
        AttachmentKind kind,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageBackend,
        string storageKey,
        string? note,
        TimeProvider timeProvider)
    {
        if (OwnerCount(transactionId, pendingTransactionId, cardStatementId) > 1)
            throw new ArgumentException("An attachment belongs to at most one transaction, pending transaction or statement.");

        return new Attachment
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TransactionId = transactionId,
            PendingTransactionId = pendingTransactionId,
            CardStatementId = cardStatementId,
            Kind = kind,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageBackend = storageBackend,
            StorageKey = storageKey,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>Files a queued attachment under exactly one owner.</summary>
    public void AssignTo(Guid? transactionId, Guid? pendingTransactionId, Guid? cardStatementId)
    {
        if (!IsQueued)
            throw new InvalidOperationException("Only a queued attachment can be assigned.");
        if (OwnerCount(transactionId, pendingTransactionId, cardStatementId) != 1)
            throw new ArgumentException("An attachment is assigned to exactly one owner.");

        TransactionId = transactionId;
        PendingTransactionId = pendingTransactionId;
        CardStatementId = cardStatementId;
    }

    public static int OwnerCount(Guid? transactionId, Guid? pendingTransactionId, Guid? cardStatementId) =>
        (transactionId.HasValue ? 1 : 0) + (pendingTransactionId.HasValue ? 1 : 0) + (cardStatementId.HasValue ? 1 : 0);

    /// <summary>Hands the file from the suggestion over to the transaction it became.</summary>
    public void MoveToTransaction(Guid transactionId)
    {
        TransactionId = transactionId;
        PendingTransactionId = null;
    }
}
