using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetUser;

public sealed record GetUserInput(Guid UserId, Guid TargetUserId);

/// <summary>One account with its roles, its exceptions and where each of its permissions comes from.</summary>
[RequiresPermission(AdminPermissions.Users.Read)]
public sealed class GetUserQuery(GetUserInput input)
    : QueryBase<GetUserInput, AdminUserDetailDto>(input);
