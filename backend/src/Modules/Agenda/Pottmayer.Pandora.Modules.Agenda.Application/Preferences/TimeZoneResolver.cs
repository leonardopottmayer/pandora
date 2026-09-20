using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Preferences;

/// <summary>
/// Resolves the effective IANA time zone for a new Agenda item: the one the caller gave, else the
/// user's Identity preference, else the configured account default, else UTC as the last resort.
/// Recurrence expands in this zone, so a caller that omits it (the Assistant, an import, a direct API
/// call) still lands on the user's clock instead of UTC. Delegates the chain to Identity's
/// <see cref="IEffectiveTimeZoneResolver"/>; keeping it here spares every call site the
/// TimeZoneInfo → id conversion.
/// </summary>
internal static class TimeZoneResolver
{
    public static async Task<string> ResolveAsync(
        IEffectiveTimeZoneResolver resolver, Guid userId, string? requested, CancellationToken ct)
    {
        var zone = await resolver.ResolveAsync(userId, requested, ct);
        return zone.Id;
    }
}
