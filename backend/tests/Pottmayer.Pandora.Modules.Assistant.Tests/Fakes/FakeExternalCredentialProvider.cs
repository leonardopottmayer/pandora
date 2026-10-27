using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>
/// Returns a canned API-key outcome, so handlers can be tested without Integrations, and records which
/// account the key was asked for.
/// </summary>
internal sealed class FakeExternalCredentialProvider(Result<string> apiKeyResult) : IExternalCredentialProvider
{
    public static FakeExternalCredentialProvider WithKey(string key) =>
        new(Result<string>.Success(key));

    public static FakeExternalCredentialProvider WithoutKey() =>
        new(Result<string>.Failure(Error.NotFound("Integrations.NotConnected", "no key")));

    /// <summary>The account id of the last <see cref="GetApiKeyAsync"/> call, or null if never asked.</summary>
    public Guid? LastAccountId { get; private set; }

    public Task<Result<string>> GetApiKeyAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        LastAccountId = accountId;
        return Task.FromResult(apiKeyResult);
    }

    public Task<Result<ExternalAccessToken>> GetAccessTokenAsync(Guid userId, Guid accountId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result<PasswordCredential>> GetPasswordAsync(Guid userId, Guid accountId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
