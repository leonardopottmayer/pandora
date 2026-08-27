using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published by Identity once a user confirms their e-mail (account activation). Confirming the
/// activation link proves the user owns the address, so Channels can provision it as a verified
/// e-mail channel — the address "comes from the account itself". Broker-ready POCO.
/// </summary>
public sealed record AccountActivated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string Locale) : IIntegrationEvent;
