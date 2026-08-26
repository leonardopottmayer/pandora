using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;

/// <summary>
/// The one question this module answers for the rest of Pandora: "give me a valid credential for
/// user <c>U</c> at provider <c>P</c>." Refresh is invisible — the caller never sees a refresh token
/// and never implements the refresh dance.
/// </summary>
public interface IExternalCredentialProvider
{
    /// <summary>
    /// Returns a valid OAuth access token, refreshing transparently when the cached one is close to
    /// expiry. Fails (never throws) when the account is missing, revoked, or the refresh is rejected.
    /// </summary>
    Task<Result<ExternalAccessToken>> GetAccessTokenAsync(
        Guid userId, string provider, CancellationToken ct = default);

    /// <summary>
    /// Returns a stored API key in plaintext. No expiry, no refresh — for <c>auth_kind = api_key</c>
    /// providers such as OpenAI or Gemini.
    /// </summary>
    Task<Result<string>> GetApiKeyAsync(
        Guid userId, string provider, CancellationToken ct = default);
}
