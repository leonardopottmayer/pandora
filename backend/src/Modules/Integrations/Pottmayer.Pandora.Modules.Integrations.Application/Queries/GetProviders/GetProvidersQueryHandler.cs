using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Credentials;
using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetProviders;

/// <summary>
/// The provider catalog for settings: every provider the server can talk to — OAuth and typed-in
/// credentials alike — each listed once, with how many accounts the user has connected at it.
/// </summary>
public sealed class GetProvidersQueryHandler(
    IUnitOfWorkFactory factory,
    IEnumerable<IOAuthProvider> oauthProviders,
    CredentialProviderRegistry credentialProviders)
    : QueryHandlerBase<GetProvidersQuery, IReadOnlyList<ProviderCatalogItemDto>>
{
    protected override async Task<Result<IReadOnlyList<ProviderCatalogItemDto>>> HandleAsync(
        GetProvidersQuery request, CancellationToken cancellationToken)
    {
        var accounts = await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            return await repo.GetByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        var counts = accounts
            .GroupBy(a => a.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        int CountOf(string provider) => counts.GetValueOrDefault(provider);

        var catalog = new List<ProviderCatalogItemDto>();

        foreach (var provider in oauthProviders)
        {
            catalog.Add(new ProviderCatalogItemDto(
                provider.Name, AuthKind.OAuth.Value, DisplayName: null, provider.DefaultScopes, CountOf(provider.Name)));
        }

        foreach (var provider in credentialProviders.All)
        {
            catalog.Add(new ProviderCatalogItemDto(
                provider.Name, provider.AuthKind.Value, provider.DisplayName, DefaultScopes: [], CountOf(provider.Name)));
        }

        return Ok(catalog);
    }
}
