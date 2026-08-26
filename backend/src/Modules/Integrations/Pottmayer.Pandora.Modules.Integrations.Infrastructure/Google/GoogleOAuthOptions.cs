namespace Pottmayer.Pandora.Modules.Integrations.Infrastructure.Google;

/// <summary>
/// Google OAuth client configuration. A Google Cloud project with the OAuth consent screen configured
/// is a deployment prerequisite; the client id/secret and the exact redirect URI come from there.
/// </summary>
public sealed class GoogleOAuthOptions
{
    public const string SectionName = "Pandora:Integrations:Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The redirect URI registered in the Google Cloud console. Must match byte-for-byte between the
    /// authorization request and the code exchange, e.g.
    /// <c>https://pandora.example.com/api/v1/integrations/google/callback</c>.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Scopes requested by default. <c>openid</c>/<c>email</c> yield the id token that identifies the
    /// account; the calendar scopes cover Agenda phase 5. Tasks is added in phase 6.
    /// </summary>
    public string[] Scopes { get; set; } =
    [
        "openid",
        "email",
        "https://www.googleapis.com/auth/calendar",
        "https://www.googleapis.com/auth/calendar.events"
    ];
}
