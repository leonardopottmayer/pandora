using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Contracts;

/// <summary>
/// A refresh was rejected by the provider (<c>invalid_grant</c>): the user revoked access, or the
/// grant lapsed. The account is now unusable and can only be fixed by reconnecting. Published so the
/// user can be told — a background job cannot ask for consent.
/// </summary>
[IntegrationEventName("integrations.external-account-revoked")]
public sealed record ExternalAccountRevoked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    Guid ExternalAccountId,
    string Provider) : IIntegrationEvent;
