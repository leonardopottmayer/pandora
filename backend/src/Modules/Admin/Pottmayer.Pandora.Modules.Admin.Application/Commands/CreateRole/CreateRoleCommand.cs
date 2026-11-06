using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.CreateRole;

/// <param name="UserId">The administrator making the change.</param>
public sealed record CreateRoleInput(Guid UserId, string Name, string? Description, IReadOnlyList<string> Patterns);

/// <summary>Creates a role with its patterns and returns its id.</summary>
[RequiresPermission(AdminPermissions.Roles.Manage)]
public sealed class CreateRoleCommand(CreateRoleInput input)
    : CommandBase<CreateRoleInput, Guid>(input);
