using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Application.Options;

namespace Pottmayer.Pandora.Modules.Identity.Application.Preferences;

/// <summary>
/// The single place the "which zone?" fallback chain lives: requested → stored preference → configured
/// default → UTC. Consumers that used to fall straight to UTC (Agenda, the assistant, Finances) now go
/// through here, so a missing preferences row resolves to the account default instead of UTC.
/// </summary>
public sealed class EffectiveTimeZoneResolver(
    IUserPreferencesReader preferences,
    IOptions<TimeZoneOptions> options) : IEffectiveTimeZoneResolver
{
    public async Task<TimeZoneInfo> ResolveAsync(Guid userId, string? requested = null, CancellationToken ct = default)
    {
        if (TryResolve(requested, out var fromRequested))
            return fromRequested;

        var prefs = await preferences.GetAsync(userId, ct);
        if (TryResolve(prefs?.TimeZone, out var fromPreference))
            return fromPreference;

        return ResolveDefault();
    }

    public TimeZoneInfo ResolveDefault()
        => TryResolve(options.Value.DefaultTimeZone, out var zone) ? zone : TimeZoneInfo.Utc;

    private static bool TryResolve(string? iana, out TimeZoneInfo zone)
    {
        if (!string.IsNullOrWhiteSpace(iana))
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(iana);
                return true;
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        zone = TimeZoneInfo.Utc;
        return false;
    }
}
