using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// An atomic movement in the ledger (fin008). In this phase it always targets one account; the
/// account balance is the signed sum of its <c>posted</c> transactions, never a stored field (D1).
/// The amount is always positive — direction comes from <see cref="Kind"/>. A <c>posted</c>
/// transaction is immutable in value/destination/kind: corrections are made with <see cref="Void"/>
/// plus a new entry, keeping the audit honest. Only cosmetic fields (description, payee, notes,
/// categories) stay editable.
/// </summary>
public sealed class Transaction : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid? AccountId { get; private set; }
    public Guid? CardStatementId { get; private set; }
    public Guid? CardId { get; private set; }
    public Guid? PaidStatementId { get; private set; }
    public TransactionKind Kind { get; private set; } = TransactionKind.Expense;
    public TransactionStatus Status { get; private set; } = TransactionStatus.Posted;
    public decimal Amount { get; private set; }
    public CurrencyCode Currency { get; private set; } = null!;
    public DateOnly OccurredOn { get; private set; }
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Set only for system-defined entries (opening balance, statement payment). When present,
    /// <see cref="Description"/> is empty and the display text is rendered from this descriptor at
    /// read time. User-entered transactions keep their text in <see cref="Description"/> and leave
    /// this null.
    /// </summary>
    public SystemDescription? SystemDescription { get; private set; }

    public string? Payee { get; private set; }
    public string? Notes { get; private set; }
    public Guid? SystemCategoryId { get; private set; }
    public Guid? UserCategoryId { get; private set; }

    public Guid? TransferGroupId { get; private set; }
    public decimal? FxRate { get; private set; }

    public Guid? InstallmentPlanId { get; private set; }
    public short? InstallmentNumber { get; private set; }

    public EntryOrigin Origin { get; private set; } = EntryOrigin.Manual;

    public Guid? ReversedTransactionId { get; private set; }

    /// <summary>Links the transaction back to the inbox entry it was created from (source = recurrence).</summary>
    public Guid? PendingTransactionId { get; private set; }

    /// <summary>Links the transaction back to the recurring template that generated it.</summary>
    public Guid? RecurringTransactionId { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public string? VoidReason { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsPosted => Status == TransactionStatus.Posted;
    public bool IsVoid => Status == TransactionStatus.Void;

    /// <summary>Signed contribution of this entry to the account balance (0 unless posted).</summary>
    public decimal SignedAmount => IsPosted && AccountId is not null ? Amount * Kind.Sign : 0m;

    private Transaction() { }

    public static Transaction CreateAccountTransaction(
        Guid userId,
        Guid accountId,
        TransactionKind kind,
        CurrencyCode currency,
        decimal amount,
        DateOnly occurredOn,
        string description,
        string? payee,
        string? notes,
        Guid? systemCategoryId,
        Guid? userCategoryId,
        bool post,
        TimeProvider timeProvider,
        SystemDescription? systemDescription = null)
    {
        var now = timeProvider.GetUtcNow();
        return new Transaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AccountId = accountId,
            Kind = kind,
            Status = post ? TransactionStatus.Posted : TransactionStatus.Pending,
            Amount = amount,
            Currency = currency,
            OccurredOn = occurredOn,
            Description = description.Trim(),
            SystemDescription = systemDescription,
            Payee = payee,
            Notes = notes,
            SystemCategoryId = systemCategoryId,
            UserCategoryId = userCategoryId,
            PostedAt = post ? now : null,
            CreatedAt = now
        };
    }

    public static Transaction CreateStatementTransaction(
        Guid userId,
        Guid cardId,
        Guid cardStatementId,
        TransactionKind kind,
        CurrencyCode currency,
        decimal amount,
        DateOnly occurredOn,
        string description,
        string? payee,
        string? notes,
        Guid? systemCategoryId,
        Guid? userCategoryId,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new Transaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CardId = cardId,
            CardStatementId = cardStatementId,
            Kind = kind,
            Status = TransactionStatus.Posted,
            Amount = amount,
            Currency = currency,
            OccurredOn = occurredOn,
            Description = description.Trim(),
            Payee = payee,
            Notes = notes,
            SystemCategoryId = systemCategoryId,
            UserCategoryId = userCategoryId,
            PostedAt = now,
            CreatedAt = now
        };
    }

    /// <summary>
    /// One installment of a card purchase: a posted statement transaction carrying its plan id and
    /// 1-based position. All N installments are real, committed charges, each on its own statement.
    /// </summary>
    public static Transaction CreateInstallmentTransaction(
        Guid userId,
        Guid cardId,
        Guid cardStatementId,
        Guid installmentPlanId,
        short installmentNumber,
        CurrencyCode currency,
        decimal amount,
        DateOnly occurredOn,
        string description,
        string? payee,
        string? notes,
        Guid? systemCategoryId,
        Guid? userCategoryId,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new Transaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CardId = cardId,
            CardStatementId = cardStatementId,
            InstallmentPlanId = installmentPlanId,
            InstallmentNumber = installmentNumber,
            Kind = TransactionKind.Expense,
            Status = TransactionStatus.Posted,
            Amount = amount,
            Currency = currency,
            OccurredOn = occurredOn,
            Description = description.Trim(),
            Payee = payee,
            Notes = notes,
            SystemCategoryId = systemCategoryId,
            UserCategoryId = userCategoryId,
            PostedAt = now,
            CreatedAt = now
        };
    }

    public static Transaction CreateStatementPayment(
        Guid userId,
        Guid accountId,
        Guid paidStatementId,
        CurrencyCode currency,
        decimal amount,
        DateOnly occurredOn,
        string description,
        string? payee,
        string? notes,
        decimal? fxRate,
        TimeProvider timeProvider,
        Guid? systemCategoryId = null,
        SystemDescription? systemDescription = null)
    {
        var now = timeProvider.GetUtcNow();
        return new Transaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AccountId = accountId,
            PaidStatementId = paidStatementId,
            Kind = TransactionKind.CardStatementPayment,
            Status = TransactionStatus.Posted,
            Amount = amount,
            Currency = currency,
            OccurredOn = occurredOn,
            Description = description.Trim(),
            SystemDescription = systemDescription,
            Payee = payee,
            Notes = notes,
            SystemCategoryId = systemCategoryId,
            FxRate = fxRate,
            PostedAt = now,
            CreatedAt = now
        };
    }

    /// <summary>
    /// Builds the two legs of a transfer sharing a <see cref="TransferGroupId"/>: a
    /// <c>transfer-out</c> on the source and a <c>transfer-in</c> on the destination. Same currency
    /// implies equal amounts; different currencies require both amounts plus the rate. Both legs are
    /// posted immediately and must be saved together, atomically.
    /// </summary>
    public static (Transaction Out, Transaction In) CreateTransferPair(
        Guid userId,
        Guid fromAccountId,
        CurrencyCode fromCurrency,
        decimal amountOut,
        Guid toAccountId,
        CurrencyCode toCurrency,
        decimal amountIn,
        decimal? fxRate,
        DateOnly occurredOn,
        string description,
        string? notes,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        var groupId = Guid.CreateVersion7();

        Transaction Leg(Guid accountId, TransactionKind kind, CurrencyCode currency, decimal amount) =>
            new()
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                AccountId = accountId,
                Kind = kind,
                Status = TransactionStatus.Posted,
                Amount = amount,
                Currency = currency,
                OccurredOn = occurredOn,
                Description = description.Trim(),
                Notes = notes,
                TransferGroupId = groupId,
                FxRate = fxRate,
                PostedAt = now,
                CreatedAt = now
            };

        return (
            Leg(fromAccountId, TransactionKind.TransferOut, fromCurrency, amountOut),
            Leg(toAccountId, TransactionKind.TransferIn, toCurrency, amountIn));
    }

    /// <summary>Effects a scheduled entry. No-op unless currently <c>pending</c>.</summary>
    public bool Post(TimeProvider timeProvider)
    {
        if (Status != TransactionStatus.Pending) return false;
        Status = TransactionStatus.Posted;
        PostedAt = timeProvider.GetUtcNow();
        return true;
    }

    /// <summary>Cancels the entry. Terminal: a voided entry stays voided.</summary>
    public bool Void(string? reason, TimeProvider timeProvider)
    {
        if (IsVoid) return false;
        Status = TransactionStatus.Void;
        VoidReason = reason;
        VoidedAt = timeProvider.GetUtcNow();
        return true;
    }

    /// <summary>Undoes a <see cref="Void"/>: only acts on a voided entry, restoring it to <c>posted</c>.</summary>
    public bool Restore(TimeProvider timeProvider)
    {
        if (!IsVoid) return false;
        Status = TransactionStatus.Posted;
        VoidedAt = null;
        VoidReason = null;
        return true;
    }

    /// <summary>
    /// Links this transaction to the recurring-transaction template and optional pending-transaction
    /// record it was generated from. Sets <see cref="Origin"/> to <see cref="EntryOrigin.Recurrence"/>.
    /// </summary>
    public void MarkAsRecurrence(Guid recurringTransactionId, Guid? pendingTransactionId)
    {
        Origin = EntryOrigin.Recurrence;
        RecurringTransactionId = recurringTransactionId;
        PendingTransactionId = pendingTransactionId;
    }

    /// <summary>
    /// Marks a freshly-created transaction as the reversal of <paramref name="reversedTransactionId"/>:
    /// sets <see cref="Origin"/> to <see cref="EntryOrigin.Reversal"/> and links back to the original.
    /// </summary>
    public void MarkAsReversal(Guid reversedTransactionId)
    {
        Origin = EntryOrigin.Reversal;
        ReversedTransactionId = reversedTransactionId;
    }

    /// <summary>
    /// Links this transaction to the import row and inbox entry it was approved from.
    /// Sets <see cref="Origin"/> to <see cref="EntryOrigin.Import"/>.
    /// </summary>
    public void MarkAsImport(Guid pendingTransactionId)
    {
        Origin = EntryOrigin.Import;
        PendingTransactionId = pendingTransactionId;
    }

    /// <summary>Edits the cosmetic fields. Value, destination and kind are intentionally absent.</summary>
    public void UpdateDetails(
        string description, string? payee, string? notes, Guid? systemCategoryId, Guid? userCategoryId)
    {
        Description = description.Trim();
        Payee = payee;
        Notes = notes;
        SystemCategoryId = systemCategoryId;
        UserCategoryId = userCategoryId;
    }
}
