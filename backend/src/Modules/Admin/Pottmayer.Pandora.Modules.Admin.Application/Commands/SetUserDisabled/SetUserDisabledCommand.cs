using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.SetUserDisabled;

/// <param name="UserId">The administrator making the change.</param>
public sealed record SetUserDisabledInput(Guid UserId, Guid TargetUserId, bool Disabled);

/// <summary>Disables an account (it cannot sign in and its sessions end) or enables it again.</summary>
[RequiresPermission(AdminPermissions.Users.Manage)]
public sealed class SetUserDisabledCommand(SetUserDisabledInput input)
    : CommandBase<SetUserDisabledInput, bool>(input);
