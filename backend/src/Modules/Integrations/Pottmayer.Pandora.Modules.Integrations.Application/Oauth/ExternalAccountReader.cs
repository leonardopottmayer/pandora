using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Oauth;

/// <summary>Read-only account summaries for other modules. No tokens cross this boundary.</summary>
public sealed class ExternalAccountReader(IUnitOfWorkFactory factory) : IExternalAccountReader
{
    public async Task<IReadOnlyList<ExternalAccountSummary>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var accounts = await factory.ExecuteAsync(IntegrationsModule.Name, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            return await repo.GetByUserAsync(userId, token);
        }, cancellationToken: ct);

        return [.. accounts.Select(ToSummary)];
    }

    public async Task<ExternalAccountSummary?> GetAsync(Guid externalAccountId, CancellationToken ct = default)
    {
        var account = await factory.ExecuteAsync(IntegrationsModule.Name, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            return await repo.GetByIdAsync(externalAccountId, token);
        }, cancellationToken: ct);

        return account is null ? null : ToSummary(account);
    }

    private static ExternalAccountSummary ToSummary(ExternalAccount a) =>
        new(a.Id, a.UserId, a.Provider, a.AuthKind.Value, a.ProviderAccountId, a.DisplayName,
            ScopeString.Split(a.Scopes), a.Status.Value);
}
