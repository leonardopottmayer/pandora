using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class UserCategoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

    private static UserCategory NewRoot(TimeProvider time) => UserCategory.Create(
        userId: Guid.NewGuid(),
        name: "  Food  ",
        nature: TransactionNature.Expense,
        parentCategoryId: null,
        color: "#fff",
        icon: "icon",
        displayOrder: 1,
        timeProvider: time);

    [Fact]
    public void Create_trims_name_starts_active_and_is_root()
    {
        var cat = NewRoot(new FixedTimeProvider(Now));

        Assert.Equal("Food", cat.Name);
        Assert.True(cat.IsActive);
        Assert.True(cat.IsRoot);
        Assert.Equal(TransactionNature.Expense, cat.Nature);
        Assert.Equal(Now, cat.CreatedAt);
    }

    [Fact]
    public void Create_with_parent_is_not_root()
    {
        var child = UserCategory.Create(
            userId: Guid.NewGuid(),
            name: "Restaurants",
            nature: TransactionNature.Expense,
            parentCategoryId: Guid.NewGuid(),
            color: null,
            icon: null,
            displayOrder: 0,
            timeProvider: new FixedTimeProvider(Now));

        Assert.False(child.IsRoot);
    }

    [Fact]
    public void Update_changes_cosmetics_and_trims_but_keeps_nature_and_parent()
    {
        var cat = NewRoot(new FixedTimeProvider(Now));

        cat.Update("  Groceries  ", "#000", "newicon", 9);

        Assert.Equal("Groceries", cat.Name);
        Assert.Equal("#000", cat.Color);
        Assert.Equal("newicon", cat.Icon);
        Assert.Equal(9, cat.DisplayOrder);
        Assert.Equal(TransactionNature.Expense, cat.Nature); // immutable
        Assert.True(cat.IsRoot);
    }

    [Fact]
    public void Deactivate_then_activate_toggles_state()
    {
        var cat = NewRoot(new FixedTimeProvider(Now));

        cat.Deactivate();
        Assert.False(cat.IsActive);

        cat.Activate();
        Assert.True(cat.IsActive);
    }
}
