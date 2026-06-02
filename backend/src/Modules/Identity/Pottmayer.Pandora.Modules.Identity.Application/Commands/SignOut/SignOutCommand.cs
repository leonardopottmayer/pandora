using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.SignOut;

public sealed record SignOutInput(string RefreshToken);

public sealed class SignOutCommand(SignOutInput input)
    : CommandBase<SignOutInput, bool>(input);
