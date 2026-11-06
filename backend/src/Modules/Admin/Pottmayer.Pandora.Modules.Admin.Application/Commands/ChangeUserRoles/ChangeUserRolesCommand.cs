using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.ChangeUserRoles;

/// <param name="UserId">The administrator making the change.</param>
public sealed record ChangeUserRolesInput(Guid UserId, IReadOnlyList<Guid> UserIds, Guid RoleId, bool Assign);

/// <summary>Gives a role to, or takes it from, several users at once. Returns how many changed.</summary>
[RequiresPermission(AdminPermissions.Users.Manage)]
public sealed class ChangeUserRolesCommand(ChangeUserRolesInput input)
    : CommandBase<ChangeUserRolesInput, int>(input);
