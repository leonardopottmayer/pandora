using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published by Identity after a user enables MFA, requesting that a confirmation notice be sent.
/// Informational only (no token).
/// </summary>
public sealed record MfaEnabled(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string Locale) : IIntegrationEvent;
