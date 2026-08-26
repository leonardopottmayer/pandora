using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

namespace Pottmayer.Pandora.Modules.Integrations.Infrastructure.Google;

/// <summary>
/// Google's OAuth 2.0 authorization-code flow with PKCE. Owns the endpoints and the wire mapping; the
/// module's domain owns the flow. Refresh is offline (<c>access_type=offline</c>), and a rejected
/// grant surfaces as a permanent <see cref="OAuthException"/> so the account can be marked revoked.
/// </summary>
internal sealed class GoogleOAuthProvider(
    HttpClient http,
    IOptions<GoogleOAuthOptions> options,
    TimeProvider timeProvider) : IOAuthProvider
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

    private GoogleOAuthOptions Options => options.Value;

    public string Name => "google";

    public IReadOnlyList<string> DefaultScopes => Options.Scopes;

    public Uri BuildAuthorizationUrl(OAuthAuthorizationRequest request)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = Options.ClientId,
            ["redirect_uri"] = Options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', request.Scopes),
            ["state"] = request.State,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = "S256",
            // Required to actually receive a refresh token on the first consent.
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true"
        };

        var queryString = string.Join(
            '&',
            query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return new Uri($"{AuthorizationEndpoint}?{queryString}");
    }

    public async Task<OAuthTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = Options.ClientId,
            ["client_secret"] = Options.ClientSecret,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = Options.RedirectUri
        };

        var response = await PostTokenAsync(form, ct);

        var (providerAccountId, displayName) = ReadIdToken(response.IdToken);
        return new OAuthTokens(
            response.AccessToken!,
            ExpiresAt(response.ExpiresIn),
            response.RefreshToken,
            SplitScopes(response.Scope),
            providerAccountId,
            displayName);
    }

    public async Task<OAuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = Options.ClientId,
            ["client_secret"] = Options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };

        var response = await PostTokenAsync(form, ct);

        return new OAuthTokens(
            response.AccessToken!,
            ExpiresAt(response.ExpiresIn),
            response.RefreshToken, // usually null on refresh; the caller keeps the existing one
            SplitScopes(response.Scope),
            ProviderAccountId: null,
            DisplayName: null);
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token });
        // Best-effort: the caller ignores failures, so no exception mapping here.
        await http.PostAsync(RevokeEndpoint, content, ct);
    }

    private async Task<GoogleTokenResponse> PostTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
        }
        catch (HttpRequestException ex)
        {
            throw new OAuthException("Could not reach Google's token endpoint.", isPermanent: false, ex);
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // invalid_grant means the code/refresh token is spent or revoked — retrying cannot help.
            var permanent = body.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase);
            throw new OAuthException($"Google token request failed ({(int)response.StatusCode}): {body}", permanent);
        }

        var parsed = JsonSerializer.Deserialize<GoogleTokenResponse>(body);
        if (parsed?.AccessToken is null)
            throw new OAuthException("Google token response had no access token.", isPermanent: true);

        return parsed;
    }

    private DateTimeOffset ExpiresAt(int? expiresIn) =>
        timeProvider.GetUtcNow().AddSeconds(expiresIn ?? 3600);

    private static IReadOnlyList<string> SplitScopes(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? []
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Reads <c>sub</c> and <c>email</c> from the id token's payload. The token arrived directly from
    /// Google over TLS in the token response, so the claims are read without re-verifying the
    /// signature — this only extracts an account identifier, it grants nothing.
    /// </summary>
    private static (string? Sub, string? Email) ReadIdToken(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return (null, null);

        var parts = idToken.Split('.');
        if (parts.Length < 2)
            return (null, null);

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var claims = JsonSerializer.Deserialize<GoogleIdTokenClaims>(json);
            return (claims?.Sub, claims?.Email);
        }
        catch
        {
            return (null, null);
        }
    }

    private sealed record GoogleTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("expires_in")] public int? ExpiresIn { get; init; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
        [JsonPropertyName("scope")] public string? Scope { get; init; }
        [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    }

    private sealed record GoogleIdTokenClaims
    {
        [JsonPropertyName("sub")] public string? Sub { get; init; }
        [JsonPropertyName("email")] public string? Email { get; init; }
    }
}
