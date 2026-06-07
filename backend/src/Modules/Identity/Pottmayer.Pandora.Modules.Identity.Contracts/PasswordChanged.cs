using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Contracts;

/// <summary>
/// Published by Identity after a user's password changes (via reset or authenticated change),
/// requesting that a confirmation notice be sent. No token: this is informational only.
/// </summary>
public sealed record PasswordChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string Locale) : IIntegrationEvent;
