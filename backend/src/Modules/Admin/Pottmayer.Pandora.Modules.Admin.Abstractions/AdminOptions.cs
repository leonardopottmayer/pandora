namespace Pottmayer.Pandora.Modules.Admin.Abstractions;

/// <summary>Configuration for the Admin module (bound from the <c>Pandora:Admin</c> section).</summary>
public sealed class AdminOptions
{
    public const string SectionName = "Pandora:Admin";

    /// <summary>
    /// E-mails of the accounts that must hold the <c>admin</c> role. Applied at startup and when one of
    /// them is activated, so a fresh install gets its master administrator without touching the
    /// database. Blank entries (an unset environment variable) are ignored.
    /// </summary>
    public string[] BootstrapEmails { get; set; } = [];

    /// <summary>
    /// Whether the panel needs two-step verification on the caller's account. On by default; turned
    /// off in <c>appsettings.Development.json</c> so local work does not ask for a code every sign-in.
    /// </summary>
    public bool RequireMfa { get; set; } = true;
}
