using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>An in-memory account list standing in for Integrations' reader.</summary>
internal sealed class FakeExternalAccountReader(params ExternalAccountSummary[] accounts) : IExternalAccountReader
{
    public static ExternalAccountSummary ApiKey(Guid userId, string provider = "gemini") =>
        new(Guid.NewGuid(), userId, provider, "api-key", Guid.NewGuid().ToString(), "••••1234", "Key", [], "connected");

    public Task<IReadOnlyList<ExternalAccountSummary>> ListAsync(
        Guid userId, string? provider = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExternalAccountSummary>>(
            [.. accounts.Where(a => a.UserId == userId && (provider is null || a.Provider == provider))]);

    public Task<ExternalAccountSummary?> GetAsync(Guid userId, Guid externalAccountId, CancellationToken ct = default) =>
        Task.FromResult(accounts.FirstOrDefault(a => a.UserId == userId && a.Id == externalAccountId));
}
