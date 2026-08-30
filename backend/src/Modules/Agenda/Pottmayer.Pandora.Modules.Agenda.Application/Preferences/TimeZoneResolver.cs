using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Preferences;

/// <summary>
/// Resolves the effective IANA time zone for a new Agenda item: the one the caller gave, else the
/// user's Identity preference, else UTC as the last resort. Recurrence expands in this zone, so a
/// caller that omits it (the future Assistant, an import, a direct API call) still lands on the
/// user's clock instead of UTC.
/// </summary>
internal static class TimeZoneResolver
{
    public static async Task<string> ResolveAsync(
        IUserPreferencesReader preferences, Guid userId, string? requested, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return requested;

        var prefs = await preferences.GetAsync(userId, ct);
        return prefs?.TimeZone ?? "UTC";
    }
}
