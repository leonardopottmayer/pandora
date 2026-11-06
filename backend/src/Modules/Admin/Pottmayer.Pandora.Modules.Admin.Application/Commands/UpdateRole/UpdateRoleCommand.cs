using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.UpdateRole;

/// <param name="UserId">The administrator making the change.</param>
public sealed record UpdateRoleInput(Guid UserId, Guid RoleId, string Name, string? Description, IReadOnlyList<string> Patterns);

/// <summary>Renames a role and replaces its patterns. The admin role cannot change.</summary>
[RequiresPermission(AdminPermissions.Roles.Manage)]
public sealed class UpdateRoleCommand(UpdateRoleInput input)
    : CommandBase<UpdateRoleInput, bool>(input);
