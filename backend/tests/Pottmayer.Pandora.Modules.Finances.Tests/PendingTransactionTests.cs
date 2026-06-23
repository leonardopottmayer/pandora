using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class PendingTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 6, 13);

    private static PendingTransaction NewFromRecurrence(TimeProvider time) =>
        PendingTransaction.CreateFromRecurrence(
            userId: Guid.NewGuid(),
            recurringTransactionId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            cardId: null,
            kind: "expense",
            amount: 50m,
            currency: "BRL",
            occurredOn: Today,
            description: "Netflix",
            payee: null,
            systemCategoryId: null,
            userCategoryId: null,
            suggestedStatementId: null,
            originalPayload: "{\"amount\":50}",
            timeProvider: time);

    [Fact]
    public void CreateFromRecurrence_starts_pending_with_recurrence_source()
    {
        var pending = NewFromRecurrence(new FixedTimeProvider(Now));

        Assert.True(pending.IsPending);
        Assert.Equal(EntryOrigin.Recurrence, pending.Source);
        Assert.False(pending.IsImportSource);
        Assert.Equal(Now, pending.CreatedAt);
    }

    [Fact]
    public void CreateFromImport_marks_import_source()
    {
        var pending = PendingTransaction.CreateFromImport(
            userId: Guid.NewGuid(),
            importRowId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            cardId: null,
            kind: "expense",
            amount: 30m,
            currency: "BRL",
            occurredOn: Today,
            description: "Market",
            payee: null,
            suggestedStatementId: null,
            dedupStatus: DedupStatus.New,
            duplicateOfTransactionId: null,
            duplicateOfPendingId: null,
            installmentNumber: null,
            installmentCount: null,
            originalPayload: "{}",
            timeProvider: new FixedTimeProvider(Now));

        Assert.True(pending.IsImportSource);
        Assert.Equal(EntryOrigin.Import, pending.Source);
        Assert.Equal(DedupStatus.New, pending.DedupStatus);
    }

    [Fact]
    public void Approve_links_transaction_and_is_terminal()
    {
        var time = new FixedTimeProvider(Now);
        var pending = NewFromRecurrence(time);
        var txId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        Assert.True(pending.Approve(txId, userId, time));
        Assert.False(pending.IsPending);
        Assert.Equal(PendingTransactionStatus.Approved, pending.Status);
        Assert.Equal(txId, pending.TransactionId);
        Assert.Equal(userId, pending.DecidedBy);
        Assert.Equal(Now, pending.DecidedAt);

        // Already decided → no further transition.
        Assert.False(pending.Approve(Guid.NewGuid(), userId, time));
        Assert.False(pending.Reject("x", userId, time));
    }

    [Fact]
    public void Reject_records_reason_and_is_terminal()
    {
        var time = new FixedTimeProvider(Now);
        var pending = NewFromRecurrence(time);

        Assert.True(pending.Reject("not mine", Guid.NewGuid(), time));
        Assert.Equal(PendingTransactionStatus.Rejected, pending.Status);
        Assert.Equal("not mine", pending.RejectionReason);

        Assert.False(pending.Reject("again", Guid.NewGuid(), time));
        Assert.False(pending.Approve(Guid.NewGuid(), Guid.NewGuid(), time));
    }

    [Fact]
    public void MarkLinkedToExisting_rejects_as_matched_without_creating_a_transaction()
    {
        var time = new FixedTimeProvider(Now);
        var pending = NewFromRecurrence(time);
        var existingTxId = Guid.NewGuid();

        Assert.True(pending.MarkLinkedToExisting(existingTxId, Guid.NewGuid(), time));
        Assert.Equal(PendingTransactionStatus.Rejected, pending.Status);
        Assert.Equal(DedupStatus.Matched, pending.DedupStatus);
        Assert.Equal(existingTxId, pending.DuplicateOfTransactionId);
        Assert.Equal("linked-to-existing-transaction", pending.RejectionReason);
        Assert.Null(pending.TransactionId); // no new transaction was created

        Assert.False(pending.MarkLinkedToExisting(Guid.NewGuid(), Guid.NewGuid(), time));
    }

    [Fact]
    public void UpdatePayload_edits_payload_and_never_touches_original_snapshot()
    {
        var pending = NewFromRecurrence(new FixedTimeProvider(Now));
        var originalSnapshot = pending.OriginalPayload;

        pending.UpdatePayload(
            kind: "income",
            amount: 99m,
            occurredOn: Today.AddDays(1),
            description: "  Edited  ",
            payee: "P",
            notes: "N",
            systemCategoryId: Guid.NewGuid(),
            userCategoryId: Guid.NewGuid(),
            suggestedStatementId: Guid.NewGuid());

        Assert.Equal("income", pending.Kind);
        Assert.Equal(99m, pending.Amount);
        Assert.Equal("Edited", pending.Description);
        Assert.Equal(originalSnapshot, pending.OriginalPayload); // immutable
    }
}
