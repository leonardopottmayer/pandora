using MimeKit;
using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Xunit;

namespace Pottmayer.Pandora.Modules.Communications.Tests;

public sealed class MessageComposerTests
{
    private const string Me = "me@titan.test";

    private static readonly MailAddress From = new("Me", Me);

    [Fact]
    public void Build_carries_every_part_of_the_message()
    {
        var raw = MessageComposer.Build(Outgoing() with
        {
            To = [new MailAddress("Ana", "ana@mail.test")],
            Cc = [new MailAddress(null, "bo@mail.test")],
            Bcc = [new MailAddress(null, "hidden@mail.test")],
            HtmlBody = "<p>Hello</p>",
            Attachments = [new OutgoingAttachment("file.pdf", "application/pdf", "%PDF"u8.ToArray())],
            InReplyTo = "parent@mail.test",
            References = ["root@mail.test", "parent@mail.test"],
        });

        var message = Load(raw);
        Assert.Equal("key@titan.test", message.MessageId);
        Assert.Equal(Me, message.From.Mailboxes.Single().Address);
        Assert.Equal("ana@mail.test", message.To.Mailboxes.Single().Address);
        Assert.Equal("bo@mail.test", message.Cc.Mailboxes.Single().Address);
        Assert.Equal("hidden@mail.test", message.Bcc.Mailboxes.Single().Address);
        Assert.Equal("Hello", message.TextBody);
        Assert.Equal("<p>Hello</p>", message.HtmlBody);
        Assert.Equal("parent@mail.test", message.InReplyTo);
        Assert.Equal(["root@mail.test", "parent@mail.test"], message.References);
        Assert.Equal("file.pdf", Assert.Single(message.Attachments).ContentDisposition!.FileName);
    }

    [Fact]
    public void A_line_break_in_a_field_cannot_add_a_header()
    {
        var raw = MessageComposer.Build(Outgoing() with
        {
            Subject = "Hi\r\nBcc: evil@mail.test",
            To = [new MailAddress("Ana\r\nBcc: evil2@mail.test", "ana@mail.test")],
        });

        var message = Load(raw);
        Assert.Empty(message.Bcc);
        Assert.Equal("Hi Bcc: evil@mail.test", message.Subject);
        Assert.DoesNotContain(message.Headers, h => h.Field == "Bcc");
    }

    [Theory]
    [InlineData("ana@mail.test", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("not an address", false)]
    [InlineData("no-at-sign", false)]
    [InlineData("ana@mail.test\r\nBcc: evil@mail.test", false)]
    [InlineData("Ana <ana@mail.test>", false)]
    public void Addresses_are_checked(string? address, bool valid) =>
        Assert.Equal(valid, MessageComposer.IsValidAddress(address));

    [Fact]
    public void A_reply_goes_to_reply_to_when_there_is_one()
    {
        var draft = MessageComposer.DraftFrom(Original(replyTo: "list@mail.test"), Me, ComposeMode.Reply);

        Assert.Equal(["list@mail.test"], draft.To.Select(a => a.Address));
        Assert.Empty(draft.Cc);
    }

    [Fact]
    public void A_reply_goes_to_the_sender_otherwise()
    {
        var draft = MessageComposer.DraftFrom(Original(), Me, ComposeMode.Reply);

        Assert.Equal(["ana@mail.test"], draft.To.Select(a => a.Address));
        Assert.Equal("ana@mail.test", draft.OriginalFrom!.Address);
        Assert.Equal("The text", draft.OriginalText.Trim());
    }

    [Fact]
    public void Reply_all_adds_the_others_and_leaves_out_the_mailbox_in_any_case()
    {
        var draft = MessageComposer.DraftFrom(
            Original(to: ["ME@Titan.Test", "bo@mail.test", "ANA@mail.test"], cc: ["me@titan.test", "cy@mail.test", "bo@mail.test"]),
            Me, ComposeMode.ReplyAll);

        Assert.Equal(["ana@mail.test", "bo@mail.test"], draft.To.Select(a => a.Address));
        Assert.Equal(["cy@mail.test"], draft.Cc.Select(a => a.Address));
    }

    [Fact]
    public void Replying_to_your_own_message_continues_to_the_same_people()
    {
        var draft = MessageComposer.DraftFrom(Original(from: Me, to: ["ana@mail.test"]), Me, ComposeMode.Reply);

        Assert.Equal(["ana@mail.test"], draft.To.Select(a => a.Address));
    }

    [Theory]
    [InlineData("Hello", "Re: Hello")]
    [InlineData("Re: Hello", "Re: Hello")]
    [InlineData("RE: Hello", "RE: Hello")]
    [InlineData("Res: Olá", "Res: Olá")]
    public void A_reply_prefix_is_not_doubled(string subject, string expected) =>
        Assert.Equal(expected, MessageComposer.DraftFrom(Original(subject: subject), Me, ComposeMode.Reply).Subject);

    [Theory]
    [InlineData("Hello", "Fwd: Hello")]
    [InlineData("Fwd: Hello", "Fwd: Hello")]
    [InlineData("Enc: Olá", "Enc: Olá")]
    public void A_forward_prefix_is_not_doubled(string subject, string expected) =>
        Assert.Equal(expected, MessageComposer.DraftFrom(Original(subject: subject), Me, ComposeMode.Forward).Subject);

    [Fact]
    public void A_reply_threads_on_the_original()
    {
        var draft = MessageComposer.DraftFrom(Original(references: ["root@mail.test"]), Me, ComposeMode.Reply);

        Assert.Equal("original@mail.test", draft.InReplyTo);
        Assert.Equal(["root@mail.test", "original@mail.test"], draft.References);
    }

    [Fact]
    public void A_forward_has_no_recipients_and_no_thread_and_carries_the_files_but_not_inline_images()
    {
        var draft = MessageComposer.DraftFrom(Original(withFile: true), Me, ComposeMode.Forward);

        Assert.Empty(draft.To);
        Assert.Empty(draft.Cc);
        Assert.Null(draft.InReplyTo);
        Assert.Empty(draft.References);
        Assert.Equal("report.pdf", Assert.Single(draft.Attachments).FileName);
    }

    [Fact]
    public void A_reply_carries_no_files()
    {
        Assert.Empty(MessageComposer.DraftFrom(Original(withFile: true), Me, ComposeMode.Reply).Attachments);
    }

    [Fact]
    public void An_html_only_original_is_quoted_as_text()
    {
        var draft = MessageComposer.DraftFrom(Original(htmlOnly: true), Me, ComposeMode.Reply);

        Assert.Contains("Only html", draft.OriginalText);
        Assert.DoesNotContain("<b>", draft.OriginalText);
    }

    private static OutgoingMessage Outgoing() => new(
        From, [new MailAddress(null, "ana@mail.test")], [], [], "Hi", "Hello", null, [], "key@titan.test", null, [],
        new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));

    private static MimeMessage Load(byte[] raw)
    {
        using var stream = new MemoryStream(raw);
        return MimeMessage.Load(stream);
    }

    private static byte[] Original(
        string from = "ana@mail.test", string? replyTo = null, string[]? to = null, string[]? cc = null, string subject = "Hello",
        string[]? references = null, bool withFile = false, bool htmlOnly = false)
    {
        var message = new MimeMessage { Subject = subject, MessageId = "original@mail.test" };
        message.From.Add(new MailboxAddress("Sender", from));
        if (replyTo is not null)
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        message.To.AddRange((to ?? [Me]).Select(MailboxAddress.Parse));
        message.Cc.AddRange((cc ?? []).Select(MailboxAddress.Parse));
        foreach (var reference in references ?? [])
            message.References.Add(reference);

        var body = htmlOnly ? new BodyBuilder { HtmlBody = "<b>Only html</b>" } : new BodyBuilder { TextBody = "The text", HtmlBody = "<p>The text</p>" };
        if (withFile)
        {
            body.Attachments.Add("report.pdf", "%PDF"u8.ToArray(), new ContentType("application", "pdf"));
            var inline = body.LinkedResources.Add("logo.png", "png"u8.ToArray(), new ContentType("image", "png"));
            inline.ContentId = "logo@mail.test";
        }
        message.Body = body.ToMessageBody();

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return stream.ToArray();
    }
}
