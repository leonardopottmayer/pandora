using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.ChangePassword;

public sealed record ChangePasswordInput(Guid UserId, string CurrentPassword, string NewPassword);

public sealed class ChangePasswordCommand(ChangePasswordInput input)
    : CommandBase<ChangePasswordInput, bool>(input);
