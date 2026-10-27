using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;

/// <summary>
/// The one question this module answers for the rest of Pandora: "give me a valid credential for
/// account <c>A</c> of user <c>U</c>." Consumers bind to an account id they stored — never to "the
/// account of provider P", since a user can hold several. Refresh is invisible — the caller never sees
/// a refresh token and never implements the refresh dance.
/// </summary>
/// <remarks>
/// Every method fails (never throws) with <c>Integrations.NotConnected</c> when the account does not
/// exist or belongs to someone else — the same answer for both, so an id never reveals another user's
/// account.
/// </remarks>
public interface IExternalCredentialProvider
{
    /// <summary>
    /// Returns a valid OAuth access token, refreshing transparently when the cached one is close to
    /// expiry. Fails when the account is missing, not OAuth, revoked, or the refresh is rejected.
    /// </summary>
    Task<Result<ExternalAccessToken>> GetAccessTokenAsync(
        Guid userId, Guid accountId, CancellationToken ct = default);

    /// <summary>Returns a stored API key in plaintext (<c>auth_kind = api-key</c>: OpenAI, Gemini).</summary>
    Task<Result<string>> GetApiKeyAsync(
        Guid userId, Guid accountId, CancellationToken ct = default);

    /// <summary>Returns a stored username + password in plaintext (<c>auth_kind = password</c>: Titan, IMAP).</summary>
    Task<Result<PasswordCredential>> GetPasswordAsync(
        Guid userId, Guid accountId, CancellationToken ct = default);
}
