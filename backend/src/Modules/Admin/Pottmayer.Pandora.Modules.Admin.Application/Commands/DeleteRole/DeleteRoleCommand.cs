using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.DeleteRole;

/// <param name="UserId">The administrator making the change.</param>
public sealed record DeleteRoleInput(Guid UserId, Guid RoleId);

/// <summary>Deletes a role; its holders lose it. Neither the admin nor the default role can go.</summary>
[RequiresPermission(AdminPermissions.Roles.Manage)]
public sealed class DeleteRoleCommand(DeleteRoleInput input)
    : CommandBase<DeleteRoleInput, bool>(input);
