using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.PasswordReset;

public sealed record RequestPasswordResetInput(string Email);

public sealed class RequestPasswordResetCommand(RequestPasswordResetInput input)
    : CommandBase<RequestPasswordResetInput, bool>(input);
