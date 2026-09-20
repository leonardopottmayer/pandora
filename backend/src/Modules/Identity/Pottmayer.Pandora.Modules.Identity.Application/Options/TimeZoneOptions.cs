namespace Pottmayer.Pandora.Modules.Identity.Application.Options;

/// <summary>
/// The account-wide fallback time zone (bound from <c>Pandora:DefaultTimeZone</c>), used when a user
/// has no stored preference. It is cross-cutting rather than Identity-specific, but Identity owns
/// time-zone resolution, so the binding lives here. Defaults to UTC when unset.
/// </summary>
public sealed class TimeZoneOptions
{
    /// <summary>
    /// The top-level Pandora section: the default is a single cross-cutting key (<c>DefaultTimeZone</c>),
    /// not something scoped under a module.
    /// </summary>
    public const string SectionName = "Pandora";

    /// <summary>IANA id (e.g. "America/Sao_Paulo"). Falls back to UTC when empty or unknown.</summary>
    public string DefaultTimeZone { get; set; } = "UTC";
}
