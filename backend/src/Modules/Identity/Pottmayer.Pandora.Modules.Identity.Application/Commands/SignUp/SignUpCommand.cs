using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.SignUp;

public sealed record SignUpInput(string Name, string Username, string Email, string Password);

public sealed record SignUpResult(Guid UserId);

public sealed class SignUpCommand(SignUpInput input)
    : CommandBase<SignUpInput, SignUpResult>(input);
