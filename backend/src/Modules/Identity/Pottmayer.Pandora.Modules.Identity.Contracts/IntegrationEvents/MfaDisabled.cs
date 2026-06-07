using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published by Identity after a user disables MFA, requesting that a security notice be sent.
/// Informational only (no token).
/// </summary>
public sealed record MfaDisabled(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string Locale) : IIntegrationEvent;
