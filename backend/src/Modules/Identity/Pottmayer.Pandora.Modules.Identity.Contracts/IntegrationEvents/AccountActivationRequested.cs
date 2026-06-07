using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published by Identity after a user signs up, requesting that an activation message be sent.
/// Broker-ready POCO: no domain value objects leak across the boundary.
/// </summary>
public sealed record AccountActivationRequested(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string Token,
    string Locale) : IIntegrationEvent;
