namespace Pottmayer.Pandora.Modules.Integrations.Abstractions;

/// <summary>Module-level configuration shared across layers.</summary>
public sealed class IntegrationsOptions
{
    public const string SectionName = "Pandora:Integrations";

    /// <summary>
    /// Absolute base URL of the SPA, prepended to the stored relative <c>redirect_after</c> when the
    /// OAuth callback redirects the browser home. Leave empty when the SPA and the API share an origin
    /// (behind the reverse proxy), so a relative redirect resolves correctly on its own.
    /// </summary>
    public string SpaBaseUrl { get; set; } = string.Empty;
}
