using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published by Identity when an administrator approves an account that was waiting, so Channels can
/// tell the user they can sign in. Broker-ready POCO.
/// </summary>
public sealed record AccountApproved(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string Locale) : IIntegrationEvent;
