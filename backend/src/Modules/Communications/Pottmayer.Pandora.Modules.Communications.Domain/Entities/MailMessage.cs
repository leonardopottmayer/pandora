using Pottmayer.Pandora.Modules.Communications.Domain.Ports;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Entities;

/// <summary>
/// The mirrored header of one message. The body is not here: it is fetched on demand into
/// <see cref="MailContent"/>.
/// </summary>
public sealed class MailMessage
{
    public const int AddressMaxLength = 320;

    public Guid Id { get; private set; }
    public Guid MailboxId { get; private set; }

    /// <summary>The Gmail message id, or <c>"{label_id}:{uid}"</c> on IMAP (UIDs are per folder).</summary>
    public string ProviderMessageId { get; private set; } = string.Empty;

    /// <summary>Gmail's <c>threadId</c>, or the first Message-ID of <c>References</c> on IMAP.</summary>
    public string ThreadKey { get; private set; } = string.Empty;

    public string? Rfc822MessageId { get; private set; }
    public string? FromName { get; private set; }
    public string? FromAddress { get; private set; }
    public List<MailAddress> To { get; private set; } = [];
    public List<MailAddress> Cc { get; private set; } = [];
    public string Subject { get; private set; } = string.Empty;
    public string Snippet { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
    public bool IsUnread { get; private set; }
    public bool IsStarred { get; private set; }
    public bool HasAttachments { get; private set; }
    public long SizeBytes { get; private set; }

    private readonly List<MailMessageLabel> _labels = [];
    public IReadOnlyList<MailMessageLabel> Labels => _labels;

    private MailMessage() { }

    public static MailMessage Create(
        Guid mailboxId, string providerMessageId, string threadKey, string? rfc822MessageId,
        MailAddress? from, IEnumerable<MailAddress> to, IEnumerable<MailAddress> cc,
        string subject, string snippet, DateTimeOffset sentAt,
        bool isUnread, bool isStarred, bool hasAttachments, long sizeBytes, IEnumerable<Guid> labelIds)
    {
        var message = new MailMessage
        {
            Id = Guid.CreateVersion7(),
            MailboxId = mailboxId,
            ProviderMessageId = providerMessageId,
            ThreadKey = threadKey,
            Rfc822MessageId = rfc822MessageId,
            FromName = from?.Name,
            FromAddress = from?.Address,
            To = [.. to],
            Cc = [.. cc],
            Subject = subject,
            Snippet = snippet,
            // A Date header keeps the sender's offset; Npgsql only writes UTC to timestamptz.
            SentAt = sentAt.ToUniversalTime(),
            IsUnread = isUnread,
            IsStarred = isStarred,
            HasAttachments = hasAttachments,
            SizeBytes = sizeBytes
        };
        message.SetLabels(labelIds);
        return message;
    }

    /// <summary>A message the provider reported; <paramref name="labelIds"/> are the local label rows.</summary>
    public static MailMessage FromRemote(Guid mailboxId, RemoteMessage remote, IEnumerable<Guid> labelIds) =>
        Create(mailboxId, remote.ProviderMessageId, remote.ThreadKey, remote.Rfc822MessageId, remote.From, remote.To,
            remote.Cc, remote.Subject, remote.Snippet, remote.SentAt, remote.IsUnread, remote.IsStarred,
            remote.HasAttachments, remote.SizeBytes, labelIds);

    /// <summary>The provider's current state wins: flags, labels and the header fields it may have filled in.</summary>
    public void ApplyRemote(RemoteMessage remote, IEnumerable<Guid> labelIds)
    {
        ThreadKey = remote.ThreadKey;
        Rfc822MessageId = remote.Rfc822MessageId;
        FromName = remote.From?.Name;
        FromAddress = remote.From?.Address;
        To = [.. remote.To];
        Cc = [.. remote.Cc];
        Subject = remote.Subject;
        // IMAP has no snippet: keep the one filled when the message was opened.
        if (remote.Snippet.Length > 0)
            Snippet = remote.Snippet;
        SentAt = remote.SentAt.ToUniversalTime();
        IsUnread = remote.IsUnread;
        IsStarred = remote.IsStarred;
        HasAttachments = remote.HasAttachments;
        SizeBytes = remote.SizeBytes;
        SetLabels(labelIds);
    }

    /// <summary>Replaces the label set by difference, so an unchanged link is never deleted and re-added.</summary>
    public void SetLabels(IEnumerable<Guid> labelIds)
    {
        var wanted = labelIds.ToHashSet();
        _labels.RemoveAll(l => !wanted.Contains(l.LabelId));
        foreach (var id in wanted.Where(id => _labels.All(l => l.LabelId != id)))
            _labels.Add(new MailMessageLabel(Id, id));
    }

    /// <summary>IMAP has no snippet until the message is opened; the provider's one is never replaced.</summary>
    public void FillSnippet(string snippet)
    {
        if (Snippet.Length == 0)
            Snippet = snippet;
    }

    public void RemoveLabel(Guid labelId) => _labels.RemoveAll(l => l.LabelId == labelId);
}

/// <summary>A message carries a label (com004).</summary>
public sealed record MailMessageLabel(Guid MessageId, Guid LabelId);

public sealed record MailAddress(string? Name, string Address);
