using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetUsers;

public sealed record GetUsersInput(Guid UserId);

/// <summary>Every account with the roles it holds, newest first.</summary>
[RequiresPermission(AdminPermissions.Users.Read)]
public sealed class GetUsersQuery(GetUsersInput input)
    : QueryBase<GetUsersInput, IReadOnlyList<AdminUserDto>>(input);
