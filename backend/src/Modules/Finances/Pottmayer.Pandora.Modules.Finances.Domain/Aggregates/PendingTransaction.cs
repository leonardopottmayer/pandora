using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

public sealed class PendingTransaction : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Source { get; private set; } = "recurrence";
    public Guid? RecurringTransactionId { get; private set; }

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
    public string Status { get; private set; } = "pending";
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? TransactionId { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsPending => Status == "pending";

    private PendingTransaction() { }

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
            Source = "recurrence",
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
            Status = "pending",
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
        Status = "approved";
        TransactionId = transactionId;
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
        Status = "rejected";
        RejectionReason = reason;
        DecidedBy = decidedBy;
        DecidedAt = timeProvider.GetUtcNow();
        return true;
    }
}
