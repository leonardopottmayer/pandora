using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MimeKit;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Communications.Application.Mime;

/// <summary>
/// Reads an opened message's raw RFC 822 bytes. An attachment's id is its position among the message's
/// body parts, so the same bytes always give the same ids.
/// </summary>
internal static partial class MimeReader
{
    private const int SnippetLength = 200;

    public sealed record Body(string? TextBody, string? HtmlBody, IReadOnlyList<MailAttachment> Attachments, string Snippet);

    public static Body Read(byte[] raw)
    {
        var message = Load(raw);
        var attachments = Attachments(message)
            .Select(a => new MailAttachment(a.Id, a.Part.FileName, a.Part.ContentType.MimeType, Decode(a.Part).LongLength, a.Part.ContentId))
            .ToList();

        var text = message.TextBody ?? (message.HtmlBody is { } html ? WebUtility.HtmlDecode(Tags().Replace(html, " ")) : null);
        return new Body(message.TextBody, message.HtmlBody, attachments, Snippet(text));
    }

    public static AttachmentPart? FindAttachment(byte[] raw, string attachmentId) =>
        Attachments(Load(raw)).Where(a => a.Id == attachmentId)
                              .Select(a => new AttachmentPart(Decode(a.Part), a.Part.ContentType.MimeType, a.Part.FileName))
                              .FirstOrDefault();

    public sealed record AttachmentPart(byte[] Content, string ContentType, string? FileName);

    /// <summary>Files and inline (<c>cid:</c>) images; the text and HTML bodies are not attachments.</summary>
    private static IEnumerable<(string Id, MimePart Part)> Attachments(MimeMessage message) =>
        message.BodyParts
               .Select((part, index) => (Id: index.ToString(CultureInfo.InvariantCulture), Part: part as MimePart))
               .Where(a => a.Part is not null && (a.Part.IsAttachment || (a.Part.ContentId is not null && a.Part is not TextPart)))
               .Select(a => (a.Id, a.Part!));

    private static MimeMessage Load(byte[] raw)
    {
        using var stream = new MemoryStream(raw);
        return MimeMessage.Load(stream);
    }

    private static byte[] Decode(MimePart part)
    {
        using var stream = new MemoryStream();
        part.Content?.DecodeTo(stream);
        return stream.ToArray();
    }

    private static string Snippet(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var collapsed = Whitespace().Replace(text, " ").Trim();
        return collapsed.Length <= SnippetLength ? collapsed : collapsed[..SnippetLength];
    }

    /// <summary>Good enough for a snippet: style and script blocks, then every tag.</summary>
    [GeneratedRegex(@"<(style|script)\b.*?</\1\s*>|<[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
