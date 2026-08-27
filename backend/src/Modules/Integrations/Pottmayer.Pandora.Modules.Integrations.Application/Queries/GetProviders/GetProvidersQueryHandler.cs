using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Pandora.Modules.Integrations.Application.Oauth;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetProviders;

/// <summary>
/// The provider catalog for settings: every provider the server can talk to, and whether the user has
/// connected it. Cross-references the registered providers with the user's accounts.
/// </summary>
public sealed class GetProvidersQueryHandler(
    IUnitOfWorkFactory factory,
    IEnumerable<IOAuthProvider> providers)
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

        var byProvider = accounts.ToDictionary(a => a.Provider, StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<ProviderCatalogItemDto> catalog = [.. providers.Select(p =>
        {
            byProvider.TryGetValue(p.Name, out var account);
            return new ProviderCatalogItemDto(
                p.Name,
                p.DefaultScopes,
                account is not null,
                account?.Status.Value);
        })];

        return Ok(catalog);
    }
}
