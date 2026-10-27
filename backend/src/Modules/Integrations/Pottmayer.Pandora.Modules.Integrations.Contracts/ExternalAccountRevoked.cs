using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Contracts;

/// <summary>
/// A refresh was rejected by the provider (<c>invalid_grant</c>): the user revoked access, or the
/// grant lapsed. The account is now unusable and can only be fixed by reconnecting. Published so the
/// user can be told — a background job cannot ask for consent. <see cref="Label"/> is the user's name
/// for the account, so a user with several accounts at the provider learns which one; it is null on
/// events written before accounts had names.
/// </summary>
[IntegrationEventName("integrations.external-account-revoked")]
public sealed record ExternalAccountRevoked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    Guid ExternalAccountId,
    string Provider,
    string? Label = null) : IIntegrationEvent;
