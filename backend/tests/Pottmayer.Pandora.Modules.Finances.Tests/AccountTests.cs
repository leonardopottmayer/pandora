using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    private static Account NewAccount(TimeProvider time) => Account.Create(
        userId: Guid.NewGuid(),
        name: "  Checking  ",
        type: AccountType.Checking,
        currency: CurrencyCode.Create("brl"),
        institution: "Bank",
        description: null,
        color: null,
        icon: null,
        displayOrder: 1,
        timeProvider: time);

    [Fact]
    public void Create_trims_name_and_stamps_creation()
    {
        var time = new FixedTimeProvider(Now);

        var account = NewAccount(time);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("Checking", account.Name);
        Assert.Equal("BRL", account.Currency.Value);
        Assert.Equal(Now, account.CreatedAt);
        Assert.False(account.IsArchived);
    }

    [Fact]
    public void Update_never_changes_currency()
    {
        var account = NewAccount(new FixedTimeProvider(Now));

        account.Update("Savings", AccountType.Savings, "Other Bank", "desc", "#fff", "icon", 5);

        // Currency has no mutator and is absent from Update — it stays fixed at creation.
        Assert.Equal("BRL", account.Currency.Value);
        Assert.Equal("Savings", account.Name);
        Assert.Equal(AccountType.Savings, account.Type);
    }

    [Fact]
    public void Update_is_rejected_while_archived()
    {
        var time = new FixedTimeProvider(Now);
        var account = NewAccount(time);
        account.Archive(time);

        var changed = account.Update("New", AccountType.Cash, null, null, null, null, 0);

        Assert.False(changed);
        Assert.Equal("Checking", account.Name); // unchanged
    }

    [Fact]
    public void Archive_then_unarchive_toggles_state()
    {
        var time = new FixedTimeProvider(Now);
        var account = NewAccount(time);

        account.Archive(time);
        Assert.True(account.IsArchived);
        Assert.Equal(Now, account.ArchivedAt);

        account.Unarchive();
        Assert.False(account.IsArchived);
        Assert.Null(account.ArchivedAt);
    }
}
