using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Communications.Tests;

public sealed class ThreadKeyTests
{
    [Fact]
    public void A_reply_joins_the_thread_of_the_first_reference()
    {
        var key = ThreadKey.From("<c@x>", ["<a@x>", "<b@x>"], "fallback");

        Assert.Equal("a@x", key);
    }

    [Fact]
    public void A_message_without_references_starts_its_own_thread()
    {
        Assert.Equal("a@x", ThreadKey.From("<a@x>", null, "fallback"));
        Assert.Equal("a@x", ThreadKey.From("<a@x>", [], "fallback"));
    }

    [Fact]
    public void The_root_and_its_reply_share_a_key_with_or_without_brackets()
    {
        var root = ThreadKey.From("a@x", null, "1");
        var reply = ThreadKey.From("<b@x>", ["<a@x>"], "2");

        Assert.Equal(root, reply);
    }

    [Fact]
    public void Blank_references_are_skipped()
    {
        Assert.Equal("a@x", ThreadKey.From("<b@x>", [" ", "<>", "<a@x>"], "fallback"));
    }

    [Fact]
    public void Without_any_message_id_the_fallback_is_used()
    {
        Assert.Equal("INBOX:42", ThreadKey.From(null, null, "INBOX:42"));
        Assert.Equal("INBOX:42", ThreadKey.From("  ", [""], "INBOX:42"));
    }
}
