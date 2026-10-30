using System.Globalization;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Communications.Application.Dtos;

/// <summary>Backfill progress is <see cref="MessageCount"/> / <see cref="MessageTotalEstimate"/>.</summary>
public sealed record MailboxDto(
    Guid Id,
    string Provider,
    Guid ExternalAccountId,
    string Address,
    string? DisplayName,
    string? ImapHost,
    int? ImapPort,
    string? SmtpHost,
    int? SmtpPort,
    string Status,
    string? LastError,
    DateTimeOffset? LastSyncedAt,
    int MessageCount,
    int? MessageTotalEstimate,
    DateTimeOffset? BackfillCompletedAt)
{
    public static MailboxDto From(Mailbox m, int messageCount) => new(
        m.Id, m.Provider.Value, m.ExternalAccountId, m.Address, m.DisplayName,
        m.ImapHost, m.ImapPort, m.SmtpHost, m.SmtpPort,
        m.Status.Value, m.LastError, m.LastSyncedAt,
        messageCount, m.MessageTotalEstimate, m.BackfillCompletedAt);
}

public sealed record LabelDto(Guid Id, string Name, string Kind, string? Role, bool IsExclusive, int Total, int Unread)
{
    public static LabelDto From(MailLabel l, (int Total, int Unread) counts) =>
        new(l.Id, l.Name, l.Kind.Value, l.Role?.Value, l.IsExclusive, counts.Total, counts.Unread);
}

public sealed record MessageSummaryDto(
    Guid Id,
    Guid MailboxId,
    string ThreadKey,
    MailAddress? From,
    IReadOnlyList<MailAddress> To,
    IReadOnlyList<MailAddress> Cc,
    string Subject,
    string Snippet,
    DateTimeOffset SentAt,
    bool IsUnread,
    bool IsStarred,
    bool HasAttachments,
    IReadOnlyList<Guid> LabelIds)
{
    public static MessageSummaryDto FromMessage(MailMessage m) => new(
        m.Id, m.MailboxId, m.ThreadKey,
        m.FromAddress is null ? null : new MailAddress(m.FromName, m.FromAddress),
        m.To, m.Cc, m.Subject, m.Snippet, m.SentAt, m.IsUnread, m.IsStarred, m.HasAttachments,
        [.. m.Labels.Select(l => l.LabelId)]);
}

/// <summary><see cref="NextCursor"/> goes back as <c>before</c>; null on the last page.</summary>
public sealed record MessagePageDto(IReadOnlyList<MessageSummaryDto> Items, string? NextCursor);

/// <summary>The HTML is as received: the client sanitizes it before rendering.</summary>
public sealed record MessageDto(
    MessageSummaryDto Message,
    string? Rfc822MessageId,
    string? TextBody,
    string? HtmlBody,
    IReadOnlyList<MailAttachment> Attachments);

public sealed record AttachmentFile(byte[] Content, string ContentType, string? FileName);

/// <summary>The keyset of the message list: the last row's (<c>sent_at</c>, <c>id</c>), as <c>"{utcTicks}.{id}"</c>.</summary>
public static class MessageCursor
{
    public static string Encode(MailMessage m) =>
        $"{m.SentAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}.{m.Id:N}";

    public static bool TryDecode(string? cursor, out (DateTimeOffset SentAt, Guid Id) value)
    {
        value = default;
        var parts = cursor?.Split('.');
        if (parts is not [var ticks, var id]
            || !long.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out var utcTicks)
            || utcTicks > DateTimeOffset.MaxValue.UtcTicks
            || !Guid.TryParseExact(id, "N", out var guid))
            return false;

        value = (new DateTimeOffset(utcTicks, TimeSpan.Zero), guid);
        return true;
    }
}
