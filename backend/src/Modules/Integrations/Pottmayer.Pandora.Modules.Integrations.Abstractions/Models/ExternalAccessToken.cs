namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;

/// <summary>
/// A short-lived access token handed to a consumer for one call. Transient: never persisted by the
/// consumer, never logged. The refresh token that produced it never leaves the module.
/// </summary>
public sealed record ExternalAccessToken(
    string Token,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> Scopes);
