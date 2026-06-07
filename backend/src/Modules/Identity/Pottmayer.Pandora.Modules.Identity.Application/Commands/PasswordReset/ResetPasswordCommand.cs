using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.PasswordReset;

public sealed record ResetPasswordInput(string Token, string NewPassword);

public sealed class ResetPasswordCommand(ResetPasswordInput input)
    : CommandBase<ResetPasswordInput, bool>(input);
