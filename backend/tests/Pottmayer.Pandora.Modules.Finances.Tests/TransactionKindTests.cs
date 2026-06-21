using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class TransactionKindTests
{
    [Theory]
    [InlineData("opening-balance", +1)]
    [InlineData("income", +1)]
    [InlineData("expense", -1)]
    [InlineData("transfer-in", +1)]
    [InlineData("transfer-out", -1)]
    [InlineData("investment-contribution", -1)]
    [InlineData("investment-redemption", +1)]
    [InlineData("yield", +1)]
    [InlineData("adjustment", +1)]
    [InlineData("refund", +1)]
    [InlineData("card-statement-payment", -1)]
    public void Sign_matches_balance_direction(string value, int expectedSign)
    {
        Assert.Equal(expectedSign, TransactionKind.FromValue(value).Sign);
    }

    [Theory]
    [InlineData("expense", true)]
    [InlineData("income", true)]
    [InlineData("nonsense", false)]
    [InlineData(null, false)]
    public void IsSupported_recognizes_known_values(string? value, bool supported)
    {
        Assert.Equal(supported, TransactionKind.IsSupported(value));
    }

    [Fact]
    public void FromValue_throws_on_unknown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TransactionKind.FromValue("teleport"));
    }

    [Fact]
    public void IsTransferLeg_only_for_transfer_kinds()
    {
        Assert.True(TransactionKind.TransferIn.IsTransferLeg);
        Assert.True(TransactionKind.TransferOut.IsTransferLeg);
        Assert.False(TransactionKind.Expense.IsTransferLeg);
        Assert.False(TransactionKind.Income.IsTransferLeg);
    }

    [Fact]
    public void RequiresInvestmentAccount_only_for_investment_kinds()
    {
        Assert.True(TransactionKind.InvestmentContribution.RequiresInvestmentAccount);
        Assert.True(TransactionKind.InvestmentRedemption.RequiresInvestmentAccount);
        Assert.True(TransactionKind.Yield.RequiresInvestmentAccount);
        Assert.False(TransactionKind.Expense.RequiresInvestmentAccount);
    }

    [Theory]
    [InlineData("expense", +1)]
    [InlineData("refund", -1)]
    [InlineData("income", 0)]
    public void StatementSign_reflects_card_movement(string value, int expected)
    {
        Assert.Equal(expected, TransactionKind.FromValue(value).StatementSign);
    }

    [Fact]
    public void CanTargetStatement_only_for_expense_and_refund()
    {
        Assert.True(TransactionKind.Expense.CanTargetStatement);
        Assert.True(TransactionKind.Refund.CanTargetStatement);
        Assert.False(TransactionKind.Income.CanTargetStatement);
    }

    [Fact]
    public void ReversalKind_expense_depends_on_target()
    {
        Assert.Equal(TransactionKind.Income, TransactionKind.Expense.ReversalKind(targetsStatement: false));
        Assert.Equal(TransactionKind.Refund, TransactionKind.Expense.ReversalKind(targetsStatement: true));
    }

    [Theory]
    [InlineData("income", "expense")]
    [InlineData("refund", "expense")]
    [InlineData("investment-contribution", "investment-redemption")]
    [InlineData("investment-redemption", "investment-contribution")]
    [InlineData("card-statement-payment", "refund")]
    public void ReversalKind_has_defined_opposites(string value, string expected)
    {
        var reversal = TransactionKind.FromValue(value).ReversalKind(targetsStatement: false);
        Assert.NotNull(reversal);
        Assert.Equal(expected, reversal!.Value);
    }

    [Theory]
    [InlineData("opening-balance")]
    [InlineData("adjustment")]
    [InlineData("yield")]
    public void ReversalKind_is_null_when_there_is_no_opposite(string value)
    {
        Assert.Null(TransactionKind.FromValue(value).ReversalKind(targetsStatement: false));
    }
}
