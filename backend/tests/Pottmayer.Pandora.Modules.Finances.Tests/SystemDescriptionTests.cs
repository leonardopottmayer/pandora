using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class SystemDescriptionTests
{
    [Fact]
    public void OpeningBalance_has_stable_key_and_no_args()
    {
        var desc = SystemDescription.OpeningBalance();

        Assert.Equal("transaction.opening-balance", desc.Key);
        Assert.Empty(desc.Args);
    }

    [Fact]
    public void StatementPayment_carries_reference_month_arg()
    {
        var desc = SystemDescription.StatementPayment("2026-06");

        Assert.Equal("transaction.statement-payment", desc.Key);
        Assert.Equal(["2026-06"], desc.Args);
    }

    [Fact]
    public void Equality_compares_key_and_args()
    {
        var a = SystemDescription.StatementPayment("2026-06");
        var b = SystemDescription.Create("transaction.statement-payment", ["2026-06"]);
        var differentArg = SystemDescription.StatementPayment("2026-07");
        var differentKey = SystemDescription.Create("transaction.opening-balance", ["2026-06"]);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, differentArg);
        Assert.NotEqual(a, differentKey);
    }

    [Fact]
    public void Create_treats_null_args_as_empty()
    {
        var desc = SystemDescription.Create("some.key", null);

        Assert.Empty(desc.Args);
        Assert.Equal(SystemDescription.Create("some.key", []), desc);
    }
}
