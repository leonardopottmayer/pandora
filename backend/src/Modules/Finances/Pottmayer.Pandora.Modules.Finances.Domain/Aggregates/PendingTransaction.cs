using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// A suggested movement awaiting the user's decision before it becomes a real
/// <see cref="Transaction"/>: generated from a <see cref="RecurringTransaction"/> occurrence or from
/// an imported file row. The payload stays editable while pending; once approved, rejected, or
/// linked to an existing transaction, the decision is terminal.
/// </summary>
public sealed class PendingTransaction : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public EntryOrigin Source { get; private set; } = EntryOrigin.Recurrence;
    public Guid? RecurringTransactionId { get; private set; }

    // import provenance
    public Guid? ImportRowId { get; private set; }
    public Guid? DuplicateOfTransactionId { get; private set; }
    public Guid? DuplicateOfPendingId { get; private set; }
    public DedupStatus? DedupStatus { get; private set; }
    public short? InstallmentNumber { get; private set; }
    public short? InstallmentCount { get; private set; }
    public Guid? MatchedInstallmentPlanId { get; private set; }

    // payload (editable until decided)
    public Guid? AccountId { get; private set; }
    public Guid? CardId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public decimal? Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateOnly OccurredOn { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? Payee { get; private set; }
    public string? Notes { get; private set; }
    public Guid? SystemCategoryId { get; private set; }
    public Guid? UserCategoryId { get; private set; }
    public Guid? SuggestedStatementId { get; private set; }

    /// <summary>Immutable snapshot of the original suggestion as serialized JSON.</summary>
    public string OriginalPayload { get; private set; } = string.Empty;

    // decision
    public PendingTransactionStatus Status { get; private set; } = PendingTransactionStatus.Pending;
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? TransactionId { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsPending => Status == PendingTransactionStatus.Pending;
    public bool IsImportSource => Source == EntryOrigin.Import;

    private PendingTransaction() { }

    /// <summary>Builds a suggestion from a due occurrence of a recurring template, awaiting the user's decision.</summary>
    public static PendingTransaction CreateFromRecurrence(
        Guid userId,
        Guid recurringTransactionId,
        Guid? accountId,
        Guid? cardId,
        string kind,
        decimal? amount,
        string currency,
        DateOnly occurredOn,
        string description,
        string? payee,
        Guid? systemCategoryId,
        Guid? userCategoryId,
        Guid? suggestedStatementId,
        string originalPayload,
        TimeProvider timeProvider)
    {
        return new PendingTransaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Source = EntryOrigin.Recurrence,
            RecurringTransactionId = recurringTransactionId,
            AccountId = accountId,
            CardId = cardId,
            Kind = kind,
            Amount = amount,
            Currency = currency,
            OccurredOn = occurredOn,
            Description = description,
            Payee = payee,
            SystemCategoryId = systemCategoryId,
            UserCategoryId = userCategoryId,
            SuggestedStatementId = suggestedStatementId,
            OriginalPayload = originalPayload,
            Status = PendingTransactionStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>Builds a suggestion from an imported row, carrying its dedup outcome and installment data.</summary>
    public static PendingTransaction CreateFromImport(
        Guid userId,
        Guid importRowId,
        Guid? accountId,
        Guid? cardId,
        string kind,
        decimal amount,
        string currency,
        DateOnly occurredOn,
        string description,
        string? payee,
        Guid? suggestedStatementId,
        DedupStatus dedupStatus,
        Guid? duplicateOfTransactionId,
        Guid? duplicateOfPendingId,
        short? installmentNumber,
        short? installmentCount,
        string originalPayload,
        TimeProvider timeProvider)
    {
        return new PendingTransaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Source = EntryOrigin.Import,
            ImportRowId = importRowId,
            AccountId = accountId,
            CardId = cardId,
            Kind = kind,
            Amount = amount,
            Currency = currency,
            OccurredOn = occurredOn,
            Description = description,
            SuggestedStatementId = suggestedStatementId,
            DedupStatus = dedupStatus,
            DuplicateOfTransactionId = duplicateOfTransactionId,
            DuplicateOfPendingId = duplicateOfPendingId,
            InstallmentNumber = installmentNumber,
            InstallmentCount = installmentCount,
            OriginalPayload = originalPayload,
            Status = PendingTransactionStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>
    /// Replaces the editable payload. <see cref="OriginalPayload"/> is never mutated.
    /// </summary>
    public void UpdatePayload(
        string kind,
        decimal? amount,
        DateOnly occurredOn,
        string description,
        string? payee,
        string? notes,
        Guid? systemCategoryId,
        Guid? userCategoryId,
        Guid? suggestedStatementId)
    {
        Kind = kind;
        Amount = amount;
        OccurredOn = occurredOn;
        Description = description.Trim();
        Payee = payee;
        Notes = notes;
        SystemCategoryId = systemCategoryId;
        UserCategoryId = userCategoryId;
        SuggestedStatementId = suggestedStatementId;
    }

    /// <summary>
    /// Marks the pending transaction as approved and links it to the created transaction.
    /// Terminal: returns <c>false</c> if already decided.
    /// </summary>
    public bool Approve(Guid transactionId, Guid decidedBy, TimeProvider timeProvider)
    {
        if (!IsPending) return false;
        Status = PendingTransactionStatus.Approved;
        TransactionId = transactionId;
        DecidedBy = decidedBy;
        DecidedAt = timeProvider.GetUtcNow();
        return true;
    }

    /// <summary>Reason recorded by <see cref="MarkLinkedToExisting"/>.</summary>
    public const string LinkedToExistingTransactionReason = "linked-to-existing-transaction";

    /// <summary>
    /// Resolves the suggestion by linking it to an existing transaction the user identified as the
    /// same movement (no new transaction is created). Terminal like <see cref="Reject"/>, but records
    /// the matched transaction so the relationship is auditable. Returns <c>false</c> if already decided.
    /// </summary>
    public bool MarkLinkedToExisting(Guid transactionId, Guid decidedBy, TimeProvider timeProvider)
    {
        if (!IsPending) return false;
        Status = PendingTransactionStatus.Rejected;
        DedupStatus = DedupStatus.Matched;
        DuplicateOfTransactionId = transactionId;
        RejectionReason = LinkedToExistingTransactionReason;
        DecidedBy = decidedBy;
        DecidedAt = timeProvider.GetUtcNow();
        return true;
    }

    /// <summary>
    /// Marks the pending transaction as rejected. Terminal: returns <c>false</c> if already decided.
    /// </summary>
    public bool Reject(string? reason, Guid decidedBy, TimeProvider timeProvider)
    {
        if (!IsPending) return false;
        Status = PendingTransactionStatus.Rejected;
        RejectionReason = reason;
        DecidedBy = decidedBy;
        DecidedAt = timeProvider.GetUtcNow();
        return true;
    }
}
