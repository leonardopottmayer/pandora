using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;

public sealed record SignInInput(string EmailOrUsername, string Password);

public sealed class SignInCommand(SignInInput input)
    : CommandBase<SignInInput, SignInResultDto>(input);
