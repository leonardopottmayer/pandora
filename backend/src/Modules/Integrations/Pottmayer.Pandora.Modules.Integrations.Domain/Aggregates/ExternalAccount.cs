using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;

/// <summary>
/// One connected third-party account. Holds the encrypted credentials Pandora uses on the user's
/// behalf. The tokens are opaque here: this aggregate never encrypts or decrypts — it stores what the
/// application layer already protected, and reasons about lifecycle by expiry and status, not by
/// reading the secrets.
/// </summary>
public sealed class ExternalAccount : AggregateRoot<Guid>, IAuditable
{
    private const int MaxErrorLength = 1000;

    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = null!;
    public AuthKind AuthKind { get; private set; } = null!;
    public string ProviderAccountId { get; private set; } = null!;
    public string? DisplayName { get; private set; }

    /// <summary>Granted scopes as stored, so a new feature can detect it needs re-consent.</summary>
    public string Scopes { get; private set; } = string.Empty;

    /// <summary>Protected access token (OAuth) or protected API key (api_key). Never plaintext.</summary>
    public string? AccessTokenEnc { get; private set; }

    /// <summary>Null for api_key, which has no expiry.</summary>
    public DateTimeOffset? AccessTokenExpiresAt { get; private set; }

    /// <summary>Protected refresh token. Null when the provider issues none, and always null for api_key.</summary>
    public string? RefreshTokenEnc { get; private set; }

    public AccountStatus Status { get; private set; } = null!;
    public DateTimeOffset ConnectedAt { get; private set; }
    public DateTimeOffset? LastRefreshedAt { get; private set; }
    public string? LastError { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private ExternalAccount() { }

    /// <summary>Records a freshly authorized OAuth account. Tokens arrive already protected.</summary>
    public static ExternalAccount ConnectOAuth(
        Guid userId,
        string provider,
        string providerAccountId,
        string? displayName,
        string scopes,
        string accessTokenEnc,
        DateTimeOffset accessTokenExpiresAt,
        string? refreshTokenEnc,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new ExternalAccount
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Provider = provider,
            AuthKind = AuthKind.OAuth,
            ProviderAccountId = providerAccountId,
            DisplayName = displayName,
            Scopes = scopes,
            AccessTokenEnc = accessTokenEnc,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshTokenEnc = refreshTokenEnc,
            Status = AccountStatus.Connected,
            ConnectedAt = now,
            CreatedAt = now
        };
    }

    /// <summary>
    /// Re-runs the authorization for an existing account: a reconnect that may widen scopes. Clears a
    /// previous revoke, since the user just granted consent again. Keeps the old refresh token when the
    /// provider returned none (Google omits it on a re-consent that reuses the existing grant).
    /// </summary>
    public void ReconnectOAuth(
        string? displayName,
        string scopes,
        string accessTokenEnc,
        DateTimeOffset accessTokenExpiresAt,
        string? refreshTokenEnc,
        TimeProvider timeProvider)
    {
        DisplayName = displayName;
        Scopes = scopes;
        AccessTokenEnc = accessTokenEnc;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        if (refreshTokenEnc is not null)
            RefreshTokenEnc = refreshTokenEnc;
        Status = AccountStatus.Connected;
        LastError = null;
        LastRefreshedAt = timeProvider.GetUtcNow();
        ConnectedAt = timeProvider.GetUtcNow();
    }

    /// <summary>Whether the cached access token is missing or within <paramref name="margin"/> of expiry.</summary>
    public bool NeedsRefresh(DateTimeOffset now, TimeSpan margin) =>
        AccessTokenEnc is null || AccessTokenExpiresAt is null || AccessTokenExpiresAt.Value - now <= margin;

    /// <summary>Applies the result of a successful refresh. Some providers rotate the refresh token; keep the new one when present.</summary>
    public void ApplyRefreshedTokens(
        string accessTokenEnc,
        DateTimeOffset accessTokenExpiresAt,
        string? refreshTokenEnc,
        TimeProvider timeProvider)
    {
        AccessTokenEnc = accessTokenEnc;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        if (refreshTokenEnc is not null)
            RefreshTokenEnc = refreshTokenEnc;
        Status = AccountStatus.Connected;
        LastError = null;
        LastRefreshedAt = timeProvider.GetUtcNow();
    }

    /// <summary>The refresh was rejected for good. The account is dead until reconnected.</summary>
    public void MarkRevoked(string error)
    {
        Status = AccountStatus.Revoked;
        LastError = Truncate(error);
    }

    /// <summary>Access token lapsed and there is no refresh token to renew it.</summary>
    public void MarkExpired()
    {
        Status = AccountStatus.Expired;
    }

    private static string Truncate(string value) =>
        value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];
}
