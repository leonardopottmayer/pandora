using MimeKit;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Communications.Application.Mime;

public enum ComposeMode
{
    Reply,
    ReplyAll,
    Forward,
}

public sealed record OutgoingAttachment(string FileName, string ContentType, byte[] Content);

/// <param name="MessageId">Without the angle brackets; the form's send key makes it, so a retry is the same message.</param>
public sealed record OutgoingMessage(
    MailAddress From,
    IReadOnlyList<MailAddress> To,
    IReadOnlyList<MailAddress> Cc,
    IReadOnlyList<MailAddress> Bcc,
    string Subject,
    string TextBody,
    string? HtmlBody,
    IReadOnlyList<OutgoingAttachment> Attachments,
    string MessageId,
    string? InReplyTo,
    IReadOnlyList<string> References,
    DateTimeOffset Date);

/// <summary>
/// The form for a reply or a forward. <see cref="OriginalText"/> is as written: the screen quotes it, in the user's language.
/// </summary>
public sealed record ComposeDraft(
    IReadOnlyList<MailAddress> To,
    IReadOnlyList<MailAddress> Cc,
    string Subject,
    string? InReplyTo,
    IReadOnlyList<string> References,
    MailAddress? OriginalFrom,
    DateTimeOffset OriginalDate,
    string OriginalText,
    IReadOnlyList<MailAttachment> Attachments);

/// <summary>Builds outgoing messages, and reads an original for what a reply or forward needs. No provider here.</summary>
internal static class MessageComposer
{
    private static readonly string[] ReplyPrefixes = ["re:", "res:"];
    private static readonly string[] ForwardPrefixes = ["fwd:", "fw:", "enc:"];

    public static bool IsValidAddress(string? address) =>
        !string.IsNullOrWhiteSpace(address)
        && !address.Any(char.IsWhiteSpace)
        && address.Contains('@')
        && MailboxAddress.TryParse(address, out var parsed)
        && string.Equals(parsed.Address, address, StringComparison.OrdinalIgnoreCase);

    /// <summary>The bytes to send. Bcc stays in them: SMTP takes it off the wire, and the sent copy keeps it.</summary>
    public static byte[] Build(OutgoingMessage input)
    {
        var message = new MimeMessage
        {
            MessageId = input.MessageId,
            Subject = OneLine(input.Subject),
            Date = input.Date,
        };
        message.From.Add(Mailbox(input.From));
        message.To.AddRange(input.To.Select(Mailbox));
        message.Cc.AddRange(input.Cc.Select(Mailbox));
        message.Bcc.AddRange(input.Bcc.Select(Mailbox));

        if (input.InReplyTo is { } inReplyTo)
            message.InReplyTo = inReplyTo;
        foreach (var reference in input.References)
            message.References.Add(reference);

        var body = new BodyBuilder { TextBody = input.TextBody, HtmlBody = input.HtmlBody };
        foreach (var attachment in input.Attachments)
            body.Attachments.Add(OneLine(attachment.FileName), attachment.Content, ContentType.Parse(attachment.ContentType));
        message.Body = body.ToMessageBody();

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return stream.ToArray();
    }

    public static ComposeDraft DraftFrom(byte[] raw, string ownAddress, ComposeMode mode)
    {
        var original = MimeReader.Load(raw);
        var from = Addresses(original.From).FirstOrDefault();
        var text = MimeReader.PlainText(original) ?? string.Empty;

        if (mode == ComposeMode.Forward)
        {
            // Inline images belong to the HTML, which the plain-text quote leaves behind.
            var files = MimeReader.Read(raw).Attachments.Where(a => a.ContentId is null).ToList();
            return new ComposeDraft([], [], Prefixed("Fwd: ", original.Subject, ForwardPrefixes), null, [],
                from, original.Date, text, files);
        }

        bool Own(MailAddress a) => string.Equals(a.Address, ownAddress, StringComparison.OrdinalIgnoreCase);

        var replyTo = Addresses(original.ReplyTo);
        if (replyTo.Count == 0)
            replyTo = Addresses(original.From);

        // Replying to what you sent yourself continues to the same people.
        var to = replyTo.All(Own) ? Addresses(original.To) : replyTo;
        var cc = new List<MailAddress>();
        if (mode == ComposeMode.ReplyAll)
        {
            if (!replyTo.All(Own))
                to = [.. to, .. Addresses(original.To)];
            cc = Addresses(original.Cc);
        }

        to = Distinct(to, Own, []);
        cc = Distinct(cc, Own, to);

        var references = original.References.ToList();
        if (original.MessageId is { } id && !references.Contains(id))
            references.Add(id);

        return new ComposeDraft(to, cc, Prefixed("Re: ", original.Subject, ReplyPrefixes), original.MessageId, references,
            from, original.Date, text, []);
    }

    private static List<MailAddress> Distinct(List<MailAddress> list, Func<MailAddress, bool> own, List<MailAddress> already) =>
        list.Where(a => !own(a))
            .Where(a => !already.Any(b => string.Equals(a.Address, b.Address, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(a => a.Address, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Prefixed(string prefix, string? subject, string[] already)
    {
        var value = subject?.Trim() ?? string.Empty;
        return already.Any(p => value.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ? value : prefix + value;
    }

    private static List<MailAddress> Addresses(InternetAddressList list) =>
        list.Mailboxes.Select(m => new MailAddress(string.IsNullOrWhiteSpace(m.Name) ? null : m.Name, m.Address)).ToList();

    private static MailboxAddress Mailbox(MailAddress address) =>
        new(address.Name is null ? null : OneLine(address.Name), address.Address);

    /// <summary>A line break in a header would start another header, chosen by whoever wrote the value.</summary>
    private static string OneLine(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
}
