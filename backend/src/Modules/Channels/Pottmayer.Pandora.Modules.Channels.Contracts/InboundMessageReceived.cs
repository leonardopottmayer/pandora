using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Contracts;

/// <summary>
/// A free-text or media message the user sent inbound, normalized and stripped of any knowledge that
/// Telegram exists. Whoever owns the conversation — the Assistant — subscribes and interprets it; the
/// media bytes are fetched on demand through <c>IInboundMediaReader</c>, never carried here.
/// </summary>
/// <param name="Bot">
/// The inbound route the message arrived on — the bot name for Telegram (e.g. <c>notifications</c>,
/// <c>assistant</c>). Lets a subscriber act only on its own route without learning the transport.
/// </param>
public sealed record InboundMessageReceived(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Channel,
    string Bot,
    string? Text,
    string? MediaRef,
    string? MediaMimeType) : IIntegrationEvent;
