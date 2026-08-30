using Pottmayer.Pandora.Modules.Identity.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

/// <summary>
/// The question Identity answers for the rest of Pandora about a user's scheduling defaults:
/// "which time zone, week start and alert offset did user <c>U</c> choose?" Consumers use it to
/// default new items (e.g. Agenda resolving the zone for a reminder when the caller gave none).
/// </summary>
public interface IUserPreferencesReader
{
    /// <summary>
    /// Returns the user's scheduling preferences, or <c>null</c> when the user or their preferences
    /// row does not exist. Never throws for a missing user — the caller falls back to its own default.
    /// </summary>
    Task<UserPreferencesSnapshot?> GetAsync(Guid userId, CancellationToken ct = default);
}
