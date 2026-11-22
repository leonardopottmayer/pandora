namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

/// <summary>
/// One per OAuth provider. The domain knows the flow (authorize → exchange → refresh → revoke); the
/// infrastructure implementation knows the provider's endpoints. Adding Microsoft is a new
/// implementation plus a registration — the domain does not change.
/// </summary>
public interface IOAuthProvider
{
    /// <summary>Provider key, e.g. <c>google</c>. Matches the <c>provider</c> column.</summary>
    string Name { get; }

    /// <summary>Scopes requested when no specific set is asked for (the module's default feature set).</summary>
    IReadOnlyList<string> DefaultScopes { get; }

    Uri BuildAuthorizationUrl(OAuthAuthorizationRequest request);

    Task<OAuthTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default);

    Task<OAuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Best-effort revoke at the provider. A failure here does not block local deletion.</summary>
    Task RevokeAsync(string token, CancellationToken ct = default);
}
