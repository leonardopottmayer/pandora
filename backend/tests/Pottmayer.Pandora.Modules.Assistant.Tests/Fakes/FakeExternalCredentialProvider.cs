using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>Returns a canned API-key outcome, so the reachability handler can be tested without Integrations.</summary>
internal sealed class FakeExternalCredentialProvider(Result<string> apiKeyResult) : IExternalCredentialProvider
{
    public static FakeExternalCredentialProvider WithKey(string key) =>
        new(Result<string>.Success(key));

    public static FakeExternalCredentialProvider WithoutKey() =>
        new(Result<string>.Failure(Error.NotFound("Integrations.NotConnected", "no key")));

    public Task<Result<string>> GetApiKeyAsync(Guid userId, string provider, CancellationToken ct = default)
        => Task.FromResult(apiKeyResult);

    public Task<Result<ExternalAccessToken>> GetAccessTokenAsync(Guid userId, string provider, CancellationToken ct = default)
        => throw new NotImplementedException();
}
