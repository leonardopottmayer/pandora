using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetCatalog;

public sealed record GetCatalogInput(Guid UserId);

/// <summary>Every declared permission, for the roles matrix.</summary>
[RequiresPermission(AdminPermissions.Roles.Read)]
public sealed class GetCatalogQuery(GetCatalogInput input)
    : QueryBase<GetCatalogInput, IReadOnlyList<CatalogModuleDto>>(input);
