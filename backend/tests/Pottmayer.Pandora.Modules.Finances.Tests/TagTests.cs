using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class TagTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_name_and_stamps_creation()
    {
        var tag = Tag.Create(Guid.NewGuid(), "  Travel  ", "#0af", new FixedTimeProvider(Now));

        Assert.NotEqual(Guid.Empty, tag.Id);
        Assert.Equal("Travel", tag.Name);
        Assert.Equal("#0af", tag.Color);
        Assert.Equal(Now, tag.CreatedAt);
    }

    [Fact]
    public void Update_changes_name_and_color()
    {
        var tag = Tag.Create(Guid.NewGuid(), "Old", null, new FixedTimeProvider(Now));

        tag.Update("  New  ", "#fff");

        Assert.Equal("New", tag.Name);
        Assert.Equal("#fff", tag.Color);
    }

    [Fact]
    public void Link_captures_the_polymorphic_target()
    {
        var tagId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var link = TagLink.Create(tagId, TaggableEntityType.Transaction, entityId, new FixedTimeProvider(Now));

        Assert.Equal(tagId, link.TagId);
        Assert.Equal(TaggableEntityType.Transaction, link.EntityType);
        Assert.Equal(entityId, link.EntityId);
        Assert.Equal(Now, link.CreatedAt);
    }

    [Theory]
    [InlineData("account", true)]
    [InlineData("card", true)]
    [InlineData("card-statement", true)]
    [InlineData("transaction", true)]
    [InlineData("recurring-transaction", true)]   // valid value now; targets arrive in phase 08
    [InlineData("pending-transaction", true)]
    [InlineData("budget", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TaggableEntityType_support_matches_the_complete_enum(string? value, bool supported)
    {
        Assert.Equal(supported, TaggableEntityType.IsSupported(value));
    }
}
