using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class TransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 6, 13);
    private static readonly CurrencyCode Brl = CurrencyCode.Create("BRL");

    private static Transaction NewAccountTx(
        TimeProvider time, TransactionKind kind, bool post = true, decimal amount = 100m) =>
        Transaction.CreateAccountTransaction(
            userId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            kind: kind,
            currency: Brl,
            amount: amount,
            occurredOn: Today,
            description: "  Groceries  ",
            payee: null,
            notes: null,
            systemCategoryId: null,
            userCategoryId: null,
            post: post,
            timeProvider: time);

    // ─── Creation ────────────────────────────────────────────────────────────

    [Fact]
    public void CreateAccountTransaction_trims_description_and_stamps_posted()
    {
        var time = new FixedTimeProvider(Now);

        var tx = NewAccountTx(time, TransactionKind.Expense);

        Assert.NotEqual(Guid.Empty, tx.Id);
        Assert.Equal("Groceries", tx.Description);
        Assert.True(tx.IsPosted);
        Assert.Equal(Now, tx.PostedAt);
        Assert.Equal(Now, tx.CreatedAt);
        Assert.Equal("manual", tx.Origin);
    }

    [Fact]
    public void CreateAccountTransaction_unposted_is_pending_with_no_posted_at()
    {
        var tx = NewAccountTx(new FixedTimeProvider(Now), TransactionKind.Expense, post: false);

        Assert.False(tx.IsPosted);
        Assert.Equal(TransactionStatus.Pending, tx.Status);
        Assert.Null(tx.PostedAt);
    }

    // ─── SignedAmount (D1: balance is the signed sum of posted transactions) ───

    [Fact]
    public void SignedAmount_applies_kind_sign_when_posted_on_an_account()
    {
        var expense = NewAccountTx(new FixedTimeProvider(Now), TransactionKind.Expense, amount: 100m);
        var income = NewAccountTx(new FixedTimeProvider(Now), TransactionKind.Income, amount: 100m);

        Assert.Equal(-100m, expense.SignedAmount);
        Assert.Equal(+100m, income.SignedAmount);
    }

    [Fact]
    public void SignedAmount_is_zero_when_not_posted()
    {
        var pending = NewAccountTx(new FixedTimeProvider(Now), TransactionKind.Income, post: false);

        Assert.Equal(0m, pending.SignedAmount);
    }

    [Fact]
    public void SignedAmount_is_zero_for_a_card_transaction_with_no_account()
    {
        // Statement transactions have no AccountId: they never move an account balance.
        var tx = Transaction.CreateStatementTransaction(
            userId: Guid.NewGuid(),
            cardId: Guid.NewGuid(),
            cardStatementId: Guid.NewGuid(),
            kind: TransactionKind.Expense,
            currency: Brl,
            amount: 50m,
            occurredOn: Today,
            description: "Card buy",
            payee: null,
            notes: null,
            systemCategoryId: null,
            userCategoryId: null,
            timeProvider: new FixedTimeProvider(Now));

        Assert.Null(tx.AccountId);
        Assert.Equal(0m, tx.SignedAmount);
    }

    // ─── Post / Void / Restore state machine ──────────────────────────────────

    [Fact]
    public void Post_effects_a_pending_entry_once()
    {
        var time = new FixedTimeProvider(Now);
        var tx = NewAccountTx(time, TransactionKind.Expense, post: false);

        Assert.True(tx.Post(time));
        Assert.True(tx.IsPosted);
        Assert.Equal(Now, tx.PostedAt);

        // Idempotent: posting an already-posted entry is a no-op.
        Assert.False(tx.Post(time));
    }

    [Fact]
    public void Void_is_terminal()
    {
        var time = new FixedTimeProvider(Now);
        var tx = NewAccountTx(time, TransactionKind.Expense);

        Assert.True(tx.Void("mistake", time));
        Assert.True(tx.IsVoid);
        Assert.Equal("mistake", tx.VoidReason);
        Assert.Equal(Now, tx.VoidedAt);
        Assert.Equal(0m, tx.SignedAmount); // a void entry drops out of the balance

        // Voiding again does nothing.
        Assert.False(tx.Void("again", time));
        Assert.Equal("mistake", tx.VoidReason);
    }

    [Fact]
    public void Restore_only_acts_on_a_voided_entry()
    {
        var time = new FixedTimeProvider(Now);
        var tx = NewAccountTx(time, TransactionKind.Expense);

        Assert.False(tx.Restore(time)); // not void → no-op

        tx.Void("oops", time);
        Assert.True(tx.Restore(time));
        Assert.True(tx.IsPosted);
        Assert.Null(tx.VoidedAt);
        Assert.Null(tx.VoidReason);
    }

    // ─── Transfer pair ────────────────────────────────────────────────────────

    [Fact]
    public void CreateTransferPair_same_currency_shares_group_and_mirrors_legs()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var (outLeg, inLeg) = Transaction.CreateTransferPair(
            userId: Guid.NewGuid(),
            fromAccountId: from,
            fromCurrency: Brl,
            amountOut: 250m,
            toAccountId: to,
            toCurrency: Brl,
            amountIn: 250m,
            fxRate: null,
            occurredOn: Today,
            description: "Move",
            notes: null,
            timeProvider: new FixedTimeProvider(Now));

        Assert.Equal(TransactionKind.TransferOut, outLeg.Kind);
        Assert.Equal(TransactionKind.TransferIn, inLeg.Kind);
        Assert.Equal(from, outLeg.AccountId);
        Assert.Equal(to, inLeg.AccountId);
        Assert.Equal(outLeg.TransferGroupId, inLeg.TransferGroupId);
        Assert.NotNull(outLeg.TransferGroupId);
        Assert.True(outLeg.IsPosted);
        Assert.True(inLeg.IsPosted);
        // Out decreases the source, In increases the destination.
        Assert.Equal(-250m, outLeg.SignedAmount);
        Assert.Equal(+250m, inLeg.SignedAmount);
    }

    [Fact]
    public void CreateTransferPair_cross_currency_keeps_distinct_amounts_and_rate()
    {
        var (outLeg, inLeg) = Transaction.CreateTransferPair(
            userId: Guid.NewGuid(),
            fromAccountId: Guid.NewGuid(),
            fromCurrency: Brl,
            amountOut: 100m,
            toAccountId: Guid.NewGuid(),
            toCurrency: CurrencyCode.Create("USD"),
            amountIn: 20m,
            fxRate: 0.2m,
            occurredOn: Today,
            description: "FX move",
            notes: null,
            timeProvider: new FixedTimeProvider(Now));

        Assert.Equal(100m, outLeg.Amount);
        Assert.Equal(20m, inLeg.Amount);
        Assert.Equal(0.2m, outLeg.FxRate);
        Assert.Equal(0.2m, inLeg.FxRate);
    }

    // ─── Provenance markers ───────────────────────────────────────────────────

    [Fact]
    public void MarkAsRecurrence_sets_origin_and_links()
    {
        var tx = NewAccountTx(new FixedTimeProvider(Now), TransactionKind.Expense);
        var recurringId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();

        tx.MarkAsRecurrence(recurringId, pendingId);

        Assert.Equal("recurrence", tx.Origin);
        Assert.Equal(recurringId, tx.RecurringTransactionId);
        Assert.Equal(pendingId, tx.PendingTransactionId);
    }

    [Fact]
    public void MarkAsReversal_sets_origin_and_links_original()
    {
        var tx = NewAccountTx(new FixedTimeProvider(Now), TransactionKind.Income);
        var originalId = Guid.NewGuid();

        tx.MarkAsReversal(originalId);

        Assert.Equal("reversal", tx.Origin);
        Assert.Equal(originalId, tx.ReversedTransactionId);
    }

    [Fact]
    public void MarkAsImport_sets_origin_and_links_pending()
    {
        var tx = NewAccountTx(new FixedTimeProvider(Now), TransactionKind.Expense);
        var pendingId = Guid.NewGuid();

        tx.MarkAsImport(pendingId);

        Assert.Equal("import", tx.Origin);
        Assert.Equal(pendingId, tx.PendingTransactionId);
    }

    // ─── UpdateDetails: cosmetic only ─────────────────────────────────────────

    [Fact]
    public void UpdateDetails_changes_cosmetics_and_leaves_value_kind_and_destination()
    {
        var tx = NewAccountTx(new FixedTimeProvider(Now), TransactionKind.Expense, amount: 100m);
        var accountId = tx.AccountId;

        tx.UpdateDetails("  New desc  ", "Payee", "note", Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal("New desc", tx.Description);
        Assert.Equal("Payee", tx.Payee);
        Assert.Equal("note", tx.Notes);
        // Untouched:
        Assert.Equal(100m, tx.Amount);
        Assert.Equal(TransactionKind.Expense, tx.Kind);
        Assert.Equal(accountId, tx.AccountId);
    }
}
