using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.RevokeUserSessions;

/// <param name="UserId">The administrator making the change.</param>
public sealed record RevokeUserSessionsInput(Guid UserId, Guid TargetUserId);

/// <summary>Ends every session of an account; it must sign in again on every device.</summary>
[RequiresPermission(AdminPermissions.Users.Manage)]
public sealed class RevokeUserSessionsCommand(RevokeUserSessionsInput input)
    : CommandBase<RevokeUserSessionsInput, bool>(input);
