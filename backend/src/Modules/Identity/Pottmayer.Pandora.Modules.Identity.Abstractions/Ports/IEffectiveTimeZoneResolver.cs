namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

/// <summary>
/// Resolves the effective IANA time zone for a user, so no consumer has to hardcode UTC when a
/// preference is missing. The chain is: the caller's requested zone (if it names a real zone) → the
/// user's Identity preference → the configured backend default (<c>Pandora:DefaultTimeZone</c>) → UTC
/// as the last resort. Never throws for a missing user or an unknown zone id.
/// </summary>
/// <remarks>
/// The configured default is what keeps a user whose first contact is a non-web channel (the assistant
/// over Telegram) — and therefore never had a browser zone captured — off the UTC fallback that made
/// "22h" fire three hours early.
/// </remarks>
public interface IEffectiveTimeZoneResolver
{
    /// <summary>
    /// The user's effective zone. <paramref name="requested"/> is an explicit override (e.g. a zone
    /// passed on an API call); when null or unknown it is ignored and the chain falls through.
    /// </summary>
    Task<TimeZoneInfo> ResolveAsync(Guid userId, string? requested = null, CancellationToken ct = default);

    /// <summary>
    /// The configured account-wide default zone, with no user in play — for global jobs that process
    /// every user in one pass and so cannot pick a single user's zone. Falls back to UTC.
    /// </summary>
    TimeZoneInfo ResolveDefault();
}
