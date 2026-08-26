using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Contracts;

/// <summary>
/// The user deliberately disconnected an account. Consumers (e.g. Agenda) disable the bindings that
/// used it, but leave the synced data in place — disconnecting Google must not delete the user's
/// events.
/// </summary>
[IntegrationEventName("integrations.external-account-disconnected")]
public sealed record ExternalAccountDisconnected(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    Guid ExternalAccountId,
    string Provider) : IIntegrationEvent;
