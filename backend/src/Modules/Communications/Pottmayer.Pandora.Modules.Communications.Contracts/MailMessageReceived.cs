using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Contracts;

/// <summary>
/// A message arrived in one of the user's mailboxes. Published only for mail that arrives after the
/// mailbox was connected (never for the backfill), and once per message: a copy or a move of a message
/// the mailbox already has (same Message-ID) is not an arrival. <see cref="Labels"/> are the provider's
/// label ids (Gmail) or the folder path (IMAP).
/// </summary>
[IntegrationEventName("communications.mail-message-received")]
public sealed record MailMessageReceived(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    Guid MailboxId,
    Guid MessageId,
    string? From,
    string Subject,
    IReadOnlyList<string> Labels) : IIntegrationEvent;
