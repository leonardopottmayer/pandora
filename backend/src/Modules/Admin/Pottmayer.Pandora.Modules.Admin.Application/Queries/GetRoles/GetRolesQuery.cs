using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetRoles;

public sealed record GetRolesInput(Guid UserId);

/// <summary>Every role, with its patterns and holder count.</summary>
[RequiresPermission(AdminPermissions.Roles.Read)]
public sealed class GetRolesQuery(GetRolesInput input)
    : QueryBase<GetRolesInput, IReadOnlyList<RoleDto>>(input);
