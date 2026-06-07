using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published by Identity when a user requests a password reset, requesting that a reset message be sent.
/// Broker-ready POCO: no domain value objects leak across the boundary.
/// </summary>
public sealed record PasswordResetRequested(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string Token,
    string Locale) : IIntegrationEvent;
