using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Contracts;

/// <summary>
/// A channel stopped working for good and was switched off: the bot was blocked, the chat is gone,
/// the address is refused. Published so the user can be told, because silence looks identical to
/// "nothing happened yet".
/// </summary>
[IntegrationEventName("channels.user-channel-disabled")]
public sealed record UserChannelDisabled(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Channel,
    string Reason) : IIntegrationEvent;
