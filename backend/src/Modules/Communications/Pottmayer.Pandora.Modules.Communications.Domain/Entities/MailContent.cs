namespace Pottmayer.Pandora.Modules.Communications.Domain.Entities;

/// <summary>
/// The cached body of an opened message, keyed by the message. The HTML is kept as received and
/// sanitized when rendered. Evicted when not accessed for <c>ContentCacheRetentionDays</c>.
/// </summary>
public sealed class MailContent
{
    public Guid MessageId { get; private set; }
    public string? TextBody { get; private set; }
    public string? HtmlBody { get; private set; }
    public List<MailAttachment> Attachments { get; private set; } = [];
    public DateTimeOffset FetchedAt { get; private set; }
    public DateTimeOffset LastAccessedAt { get; private set; }

    private MailContent() { }

    public static MailContent Create(
        Guid messageId, string? textBody, string? htmlBody, IEnumerable<MailAttachment> attachments, DateTimeOffset now)
        => new()
        {
            MessageId = messageId,
            TextBody = textBody,
            HtmlBody = htmlBody,
            Attachments = [.. attachments],
            FetchedAt = now,
            LastAccessedAt = now
        };

    /// <summary>Opened again: the retention clock starts over.</summary>
    public void Touch(DateTimeOffset now) => LastAccessedAt = now;
}

/// <summary>One part of the message. <see cref="ContentId"/> is set for inline (<c>cid:</c>) images.</summary>
public sealed record MailAttachment(string Id, string? FileName, string ContentType, long SizeBytes, string? ContentId);
