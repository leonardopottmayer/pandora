using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Users.Application.Commands.RegisterUser;

public sealed record RegisterUserInput(string Name, string Username, string Email, string Password);

public sealed record RegisterUserResult(Guid Id);

public sealed class RegisterUserCommand(RegisterUserInput input)
    : CommandBase<RegisterUserInput, RegisterUserResult>(input);
