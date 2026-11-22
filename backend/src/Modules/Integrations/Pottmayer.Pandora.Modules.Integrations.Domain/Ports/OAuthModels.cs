namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

/// <summary>
/// Everything a provider needs to build the consent URL for one authorization request. The redirect
/// URI is not here: it is a fixed, provider-registered value the implementation owns, and must match
/// byte-for-byte between the authorization request and the code exchange. <see cref="LoginHint"/> is
/// the account the user is expected to sign in with (a reconnect), or null to let them pick one.
/// </summary>
public sealed record OAuthAuthorizationRequest(
    string State,
    string CodeChallenge,
    IReadOnlyList<string> Scopes,
    string? LoginHint = null);

/// <summary>The tokens a provider returns from a code exchange or a refresh.</summary>
/// <remarks>
/// <see cref="RefreshToken"/> is null on a refresh that does not rotate it, and on a re-consent where
/// the provider reuses the existing grant. <see cref="ProviderAccountId"/> and
/// <see cref="DisplayName"/> are populated on a code exchange (from the id token / userinfo) and left
/// null on a plain refresh.
/// </remarks>
public sealed record OAuthTokens(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string? RefreshToken,
    IReadOnlyList<string> Scopes,
    string? ProviderAccountId,
    string? DisplayName);
