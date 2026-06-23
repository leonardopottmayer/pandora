using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class InstallmentPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Split_puts_rounding_remainder_on_the_first_installment_and_sums_to_total()
    {
        var parts = InstallmentPlan.SplitAmount(1000m, 3);

        Assert.Equal(new[] { 333.34m, 333.33m, 333.33m }, parts);
        Assert.Equal(1000m, parts.Sum());
    }

    [Fact]
    public void Split_with_no_remainder_is_even()
    {
        var parts = InstallmentPlan.SplitAmount(900m, 3);

        Assert.Equal(new[] { 300m, 300m, 300m }, parts);
        Assert.Equal(900m, parts.Sum());
    }

    [Theory]
    [InlineData(100.00, 7)]
    [InlineData(1234.56, 12)]
    [InlineData(0.10, 3)]
    public void Split_always_sums_back_to_the_total(decimal total, int count)
    {
        var parts = InstallmentPlan.SplitAmount(total, count);

        Assert.Equal(count, parts.Length);
        Assert.Equal(total, parts.Sum());
    }

    [Fact]
    public void Manual_plan_fills_normalized_description()
    {
        var plan = InstallmentPlan.CreateManual(
            Guid.NewGuid(), Guid.NewGuid(), 1200m, 12, "2026-06", "Loja X 03/12", new FixedTimeProvider(Now));

        Assert.Equal(EntryOrigin.Manual, plan.Origin);
        Assert.False(plan.TotalIsEstimate);
        Assert.Equal("loja x", plan.NormalizedDescription);
    }

    [Theory]
    [InlineData("Loja X 03/12", "loja x")]
    [InlineData("PARC 3/12 Mercado", "mercado")]
    [InlineData("Compra 3 de 12", "compra")]
    [InlineData("Sem parcela", "sem parcela")]
    public void Normalize_strips_installment_marker(string description, string expected)
    {
        Assert.Equal(expected, InstallmentPlan.NormalizeDescription(description));
    }
}
