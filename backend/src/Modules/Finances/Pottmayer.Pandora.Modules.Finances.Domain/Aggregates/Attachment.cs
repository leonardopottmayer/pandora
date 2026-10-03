using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// A file attached to a transaction or to an inbox suggestion — a boleto, a receipt, an invoice (fin017).
/// The bytes live in the module's <c>IFileStorage</c>; this row holds the metadata and the
/// <see cref="StorageKey"/> that locates them. It belongs to exactly one of <see cref="TransactionId"/>
/// or <see cref="PendingTransactionId"/>: a month's boleto is attached to the suggestion the recurrence
/// produced, and follows it onto the transaction when it is approved (<see cref="MoveToTransaction"/>),
/// where the receipt joins it. Write-once otherwise, so it carries just a <see cref="CreatedAt"/>.
/// </summary>
public sealed class Attachment : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? TransactionId { get; private set; }
    public Guid? PendingTransactionId { get; private set; }
    public AttachmentKind Kind { get; private set; } = AttachmentKind.Other;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    /// <summary>Which <c>IFileStorage</c> backend holds the bytes (e.g. <c>Database</c>, later <c>S3</c>).</summary>
    public string StorageBackend { get; private set; } = string.Empty;

    /// <summary>The opaque key that locates the bytes within <see cref="StorageBackend"/>.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    private Attachment() { }

    /// <summary>
    /// Records a file whose bytes are already stored under <paramref name="storageKey"/>, on exactly one
    /// owner: a transaction or a pending transaction.
    /// </summary>
    public static Attachment Create(
        Guid userId,
        Guid? transactionId,
        Guid? pendingTransactionId,
        AttachmentKind kind,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageBackend,
        string storageKey,
        TimeProvider timeProvider)
    {
        if (transactionId.HasValue == pendingTransactionId.HasValue)
            throw new ArgumentException("An attachment belongs to exactly one transaction or pending transaction.");

        return new Attachment
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TransactionId = transactionId,
            PendingTransactionId = pendingTransactionId,
            Kind = kind,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageBackend = storageBackend,
            StorageKey = storageKey,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>Hands the file from the suggestion over to the transaction it became.</summary>
    public void MoveToTransaction(Guid transactionId)
    {
        TransactionId = transactionId;
        PendingTransactionId = null;
    }
}
