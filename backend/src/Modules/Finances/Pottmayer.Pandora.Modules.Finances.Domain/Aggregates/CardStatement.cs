using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

public sealed class CardStatement : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid CardId { get; private set; }
    public string ReferenceMonth { get; private set; } = string.Empty;
    public DateOnly ClosingDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public StatementStatus Status { get; private set; } = StatementStatus.Open;
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? OverdueAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public decimal RemainingAmount => Math.Max(0m, TotalAmount - PaidAmount);
    public bool IsClosedToNewPurchases => Status != StatementStatus.Open;

    private CardStatement() { }

    public static CardStatement Create(
        Guid userId,
        Guid cardId,
        string referenceMonth,
        DateOnly closingDate,
        DateOnly dueDate,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CardId = cardId,
            ReferenceMonth = referenceMonth,
            ClosingDate = closingDate,
            DueDate = dueDate,
            Status = StatementStatus.Open,
            TotalAmount = 0m,
            PaidAmount = 0m,
            CreatedAt = timeProvider.GetUtcNow()
        };

    public bool Close(TimeProvider timeProvider)
    {
        if (Status != StatementStatus.Open) return false;
        Status = StatementStatus.Closed;
        ClosedAt = timeProvider.GetUtcNow();
        return true;
    }

    public bool Reopen(DateOnly today, TimeProvider timeProvider)
    {
        if (Status == StatementStatus.Open || Status == StatementStatus.Paid) return false;
        ClosedAt = null;
        SyncAmounts(TotalAmount, PaidAmount, today, timeProvider);
        return true;
    }

    public bool MarkOverdue(TimeProvider timeProvider)
    {
        if (RemainingAmount <= 0m || Status == StatementStatus.Paid || Status == StatementStatus.Overdue)
            return false;

        Status = StatementStatus.Overdue;
        OverdueAt ??= timeProvider.GetUtcNow();
        return true;
    }

    public void SyncAmounts(decimal totalAmount, decimal paidAmount, DateOnly today, TimeProvider timeProvider)
    {
        TotalAmount = totalAmount;
        PaidAmount = paidAmount;

        if (RemainingAmount <= 0m)
        {
            Status = StatementStatus.Paid;
            PaidAt ??= timeProvider.GetUtcNow();
            return;
        }

        PaidAt = null;

        if (today > DueDate && ClosedAt is not null)
        {
            Status = StatementStatus.Overdue;
            OverdueAt ??= timeProvider.GetUtcNow();
            return;
        }

        OverdueAt = null;

        if (ClosedAt is not null && PaidAmount > 0m)
        {
            Status = StatementStatus.PartiallyPaid;
            return;
        }

        if (ClosedAt is not null)
        {
            Status = StatementStatus.Closed;
            return;
        }

        Status = StatementStatus.Open;
    }
}
