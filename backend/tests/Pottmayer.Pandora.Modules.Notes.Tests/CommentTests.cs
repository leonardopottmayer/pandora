using Pottmayer.Pandora.Modules.Notes.Tests.Fakes;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class CommentTests
{
    private readonly FixedTimeProvider _time = new(DateTimeOffset.Parse("2026-11-07T10:00:00Z"));

    [Fact]
    public void A_block_fingerprint_ignores_its_children_and_follows_its_own_text()
    {
        static string Doc(string item, string child) =>
            $$"""{"version":1,"blocks":[{"id":"l","type":"bullet-list","content":[{"text":"{{item}}"}],"children":[{"id":"c","type":"paragraph","content":[{"text":"{{child}}"}]}]}]}""";

        var before = PageContent.Parse(Doc("Item", "Filho")).BlockFingerprints();
        var childEdited = PageContent.Parse(Doc("Item", "Outro")).BlockFingerprints();
        var itemEdited = PageContent.Parse(Doc("Item!", "Filho")).BlockFingerprints();

        Assert.Equal(["l", "c"], before.Keys);
        Assert.Equal(before["l"], childEdited["l"]);
        Assert.NotEqual(before["c"], childEdited["c"]);
        Assert.NotEqual(before["l"], itemEdited["l"]);
    }

    [Fact]
    public void A_stamp_moves_only_when_the_block_changed()
    {
        var stamp = BlockStamp.Create(Guid.NewGuid(), "b", "aaa", PageVersionSource.Edit, _time);
        _time.Advance(TimeSpan.FromHours(1));

        Assert.False(stamp.Touch("aaa", PageVersionSource.Assistant, _time));
        Assert.Equal(stamp.CreatedAt, stamp.UpdatedAt);

        Assert.True(stamp.Touch("bbb", PageVersionSource.Assistant, _time));
        Assert.Equal(TimeSpan.FromHours(1), stamp.UpdatedAt - stamp.CreatedAt);
        Assert.Equal(PageVersionSource.Assistant, stamp.UpdatedSource);
    }

    [Fact]
    public void A_reply_belongs_to_the_thread_and_resolving_keeps_the_first_moment()
    {
        var thread = PageComment.Start(Guid.NewGuid(), Guid.NewGuid(), "b", "  março ", " Abril? ", _time);
        var reply = thread.Reply("Sim", _time);

        Assert.Equal(("março", "Abril?"), (thread.Quote, thread.Body));
        Assert.True(thread.IsThread);
        Assert.Equal((thread.Id, thread.BlockId, false), (reply.ThreadId!.Value, reply.BlockId, reply.IsThread));

        thread.SetResolved(true, _time);
        var resolvedAt = thread.ResolvedAt;
        _time.Advance(TimeSpan.FromMinutes(5));
        thread.SetResolved(true, _time);
        Assert.Equal(resolvedAt, thread.ResolvedAt);
        thread.SetResolved(false, _time);
        Assert.Null(thread.ResolvedAt);
    }
}
