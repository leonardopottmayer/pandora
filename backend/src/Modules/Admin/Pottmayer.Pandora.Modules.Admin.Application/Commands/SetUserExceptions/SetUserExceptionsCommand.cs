using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.SetUserExceptions;

/// <param name="UserId">The administrator making the change.</param>
public sealed record SetUserExceptionsInput(Guid UserId, Guid TargetUserId, IReadOnlyList<UserExceptionDto> Exceptions);

/// <summary>Replaces a user's allow and deny exceptions.</summary>
[RequiresPermission(AdminPermissions.Users.Manage)]
public sealed class SetUserExceptionsCommand(SetUserExceptionsInput input)
    : CommandBase<SetUserExceptionsInput, bool>(input);
