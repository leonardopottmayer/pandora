using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Contracts;

/// <summary>
/// Asks Channels to deliver the assistant's reply straight to the user on the bot they are talking to.
/// Unlike <see cref="NotifyUserRequested"/> this is not a templated, preference-gated notification: it is
/// a plain message sent now, through the named bot, to the user's chat. Channels owns the transport — the
/// caller names the user and the bot, never an address. Broker-ready POCO.
/// </summary>
public sealed record SendAssistantReply(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Bot,
    string Text) : IIntegrationEvent;
