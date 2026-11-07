using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.ApproveUsers;

/// <param name="UserId">The administrator making the change.</param>
public sealed record ApproveUsersInput(Guid UserId, IReadOnlyList<Guid> UserIds);

/// <summary>Lets accounts waiting for approval sign in. Returns how many were waiting.</summary>
[RequiresPermission(AdminPermissions.Users.Manage)]
public sealed class ApproveUsersCommand(ApproveUsersInput input)
    : CommandBase<ApproveUsersInput, int>(input);
