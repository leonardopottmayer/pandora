using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Communications.Tests;

public sealed class LabelRoleTests
{
    [Theory]
    [InlineData("INBOX", "inbox")]
    [InlineData("SENT", "sent")]
    [InlineData("DRAFT", "drafts")]
    [InlineData("TRASH", "trash")]
    [InlineData("SPAM", "spam")]
    public void Gmail_system_labels_map_to_roles(string labelId, string role) =>
        Assert.Equal(LabelRole.FromValue(role), LabelRole.FromGmailLabelId(labelId));

    [Theory]
    [InlineData("STARRED")]
    [InlineData("UNREAD")]
    [InlineData("CATEGORY_PROMOTIONS")]
    [InlineData("Label_42")]
    public void Other_gmail_labels_have_no_role(string labelId) =>
        Assert.Null(LabelRole.FromGmailLabelId(labelId));

    [Theory]
    [InlineData("INBOX")]
    [InlineData("Inbox")]
    [InlineData("inbox")]
    public void The_imap_inbox_is_found_by_name(string path) =>
        Assert.Equal(LabelRole.Inbox, LabelRole.FromImapFolder(path, []));

    [Theory]
    [InlineData(@"\Sent", "sent")]
    [InlineData(@"\Drafts", "drafts")]
    [InlineData(@"\Trash", "trash")]
    [InlineData(@"\Junk", "spam")]
    [InlineData(@"\Archive", "archive")]
    [InlineData(@"\SENT", "sent")]
    public void Imap_folders_map_by_special_use(string attribute, string role) =>
        Assert.Equal(LabelRole.FromValue(role), LabelRole.FromImapFolder("Whatever", [@"\HasNoChildren", attribute]));

    [Fact]
    public void An_imap_folder_without_special_use_has_no_role()
    {
        Assert.Null(LabelRole.FromImapFolder("Sent Items", []));
        Assert.Null(LabelRole.FromImapFolder("All Mail", [@"\All"]));
    }
}
