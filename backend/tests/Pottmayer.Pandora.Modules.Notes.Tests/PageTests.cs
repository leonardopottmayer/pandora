using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class PageTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    private static Page NewPage(TimeProvider time, Guid? parentId = null) => Page.Create(
        userId: Guid.NewGuid(),
        title: "  My Note  ",
        slug: "my-note",
        parentId: parentId,
        icon: "📄",
        orderIndex: 0,
        contentMarkdown: "# hello",
        timeProvider: time);

    [Fact]
    public void Create_trims_title_and_stamps_creation()
    {
        var page = NewPage(new FixedTimeProvider(Now));

        Assert.NotEqual(Guid.Empty, page.Id);
        Assert.Equal("My Note", page.Title);
        Assert.Equal("my-note", page.Slug);
        Assert.Equal("# hello", page.ContentMarkdown);
        Assert.Equal(Now, page.CreatedAt);
        Assert.False(page.IsArchived);
        Assert.False(page.IsDeleted);
    }

    [Fact]
    public void Update_changes_title_icon_and_body_but_never_slug()
    {
        var page = NewPage(new FixedTimeProvider(Now));

        page.Update("Renamed", "🚀", "new body");

        Assert.Equal("Renamed", page.Title);
        Assert.Equal("🚀", page.Icon);
        Assert.Equal("new body", page.ContentMarkdown);
        Assert.Equal("my-note", page.Slug); // slug stays fixed so links survive renames
    }

    [Fact]
    public void Move_sets_parent_and_order()
    {
        var page = NewPage(new FixedTimeProvider(Now));
        var newParent = Guid.NewGuid();

        page.Move(newParent, orderIndex: 3);

        Assert.Equal(newParent, page.ParentId);
        Assert.Equal(3, page.OrderIndex);
    }

    [Fact]
    public void Move_under_self_is_coerced_to_root()
    {
        var page = NewPage(new FixedTimeProvider(Now));

        page.Move(page.Id, orderIndex: 0);

        Assert.Null(page.ParentId);
    }

    [Fact]
    public void Archive_then_unarchive_toggles_state()
    {
        var time = new FixedTimeProvider(Now);
        var page = NewPage(time);

        page.Archive(time);
        Assert.True(page.IsArchived);
        Assert.Equal(Now, page.ArchivedAt);

        page.Unarchive();
        Assert.False(page.IsArchived);
        Assert.Null(page.ArchivedAt);
    }

    [Fact]
    public void Delete_is_soft_and_idempotent()
    {
        var time = new FixedTimeProvider(Now);
        var page = NewPage(time);

        page.Delete(time);
        Assert.True(page.IsDeleted);
        Assert.Equal(Now, page.DeletedAt);

        time.Advance(TimeSpan.FromHours(1));
        page.Delete(time);
        Assert.Equal(Now, page.DeletedAt); // no-op: keeps the first timestamp
    }

    [Fact]
    public void SetFavorite_toggles_flag()
    {
        var page = NewPage(new FixedTimeProvider(Now));

        page.SetFavorite(true);
        Assert.True(page.IsFavorite);

        page.SetFavorite(false);
        Assert.False(page.IsFavorite);
    }
}
