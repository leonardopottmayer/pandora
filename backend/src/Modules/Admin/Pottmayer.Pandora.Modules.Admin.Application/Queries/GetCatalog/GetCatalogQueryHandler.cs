using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetCatalog;

public sealed class GetCatalogQueryHandler(IPermissionCatalog catalog)
    : QueryHandlerBase<GetCatalogQuery, IReadOnlyList<CatalogModuleDto>>
{
    protected override Task<Result<IReadOnlyList<CatalogModuleDto>>> HandleAsync(GetCatalogQuery request, CancellationToken ct)
    {
        IReadOnlyList<CatalogModuleDto> modules =
        [
            .. catalog.Definitions.GroupBy(d => d.Module).Select(module => new CatalogModuleDto(
                module.Key,
                [
                    .. module.GroupBy(d => d.Resource).Select(resource => new CatalogResourceDto(
                        resource.Key,
                        resource.First().Feature,
                        [.. resource.Select(d => new CatalogActionDto(d.Key, d.Action, d.Requires))]))
                ]))
        ];

        return Task.FromResult(Ok(modules));
    }
}
