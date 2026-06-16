using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// What a ledger movement represents. The amount is always positive; the direction it moves an
/// account balance is a function of the kind (<see cref="Sign"/>). This phase covers account-only
/// kinds; card kinds (<c>card-statement-payment</c>, <c>refund</c>) arrive with statements (phase 05).
/// </summary>
public sealed class TransactionKind : IDomainValue<TransactionKind>
{
    public static readonly TransactionKind OpeningBalance = new("opening-balance", sign: +1);
    public static readonly TransactionKind Income = new("income", sign: +1);
    public static readonly TransactionKind Expense = new("expense", sign: -1);
    public static readonly TransactionKind TransferIn = new("transfer-in", sign: +1);
    public static readonly TransactionKind TransferOut = new("transfer-out", sign: -1);
    public static readonly TransactionKind InvestmentContribution = new("investment-contribution", sign: -1);
    public static readonly TransactionKind InvestmentRedemption = new("investment-redemption", sign: +1);
    public static readonly TransactionKind Yield = new("yield", sign: +1);
    public static readonly TransactionKind Adjustment = new("adjustment", sign: +1);
    public static readonly TransactionKind Refund = new("refund", sign: +1);
    public static readonly TransactionKind CardStatementPayment = new("card-statement-payment", sign: -1);

    private static readonly Dictionary<string, TransactionKind> All = new()
    {
        [OpeningBalance.Value] = OpeningBalance,
        [Income.Value] = Income,
        [Expense.Value] = Expense,
        [TransferIn.Value] = TransferIn,
        [TransferOut.Value] = TransferOut,
        [InvestmentContribution.Value] = InvestmentContribution,
        [InvestmentRedemption.Value] = InvestmentRedemption,
        [Yield.Value] = Yield,
        [Adjustment.Value] = Adjustment,
        [Refund.Value] = Refund,
        [CardStatementPayment.Value] = CardStatementPayment
    };

    public string Value { get; }

    /// <summary>+1 when the kind increases the account balance, -1 when it decreases it.</summary>
    public int Sign { get; }

    private TransactionKind(string value, int sign)
    {
        Value = value;
        Sign = sign;
    }

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static TransactionKind FromValue(string value) =>
        All.TryGetValue(value, out var kind)
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown transaction kind.");

    /// <summary>The two legs of a transfer; users don't pick these directly — <c>CreateTransfer</c> does.</summary>
    public bool IsTransferLeg => this == TransferIn || this == TransferOut;

    /// <summary>Kinds that only make sense on an <c>investment</c> account (phase 04 decision: restrict).</summary>
    public bool RequiresInvestmentAccount =>
        this == InvestmentContribution || this == InvestmentRedemption || this == Yield;

    public bool CanTargetStatement => this == Expense || this == Refund;

    public bool IsStatementPayment => this == CardStatementPayment;

    public int StatementSign => this == Expense ? +1 : this == Refund ? -1 : 0;

    /// <summary>
    /// The kind a reversal of this transaction should use, or <c>null</c> when there is no defined
    /// opposite (e.g. <see cref="OpeningBalance"/>, <see cref="Adjustment"/>, <see cref="Yield"/>).
    /// <paramref name="targetsStatement"/> disambiguates <see cref="Expense"/>, which has different
    /// opposites depending on whether it targets an account (<see cref="Income"/>) or a card
    /// statement (<see cref="Refund"/>).
    /// </summary>
    public TransactionKind? ReversalKind(bool targetsStatement)
    {
        if (this == Income) return Expense;
        if (this == Expense) return targetsStatement ? Refund : Income;
        if (this == Refund) return Expense;
        if (this == InvestmentContribution) return InvestmentRedemption;
        if (this == InvestmentRedemption) return InvestmentContribution;
        if (this == CardStatementPayment) return Refund;
        return null;
    }

    public override string ToString() => Value;
}
