using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Disable;

public sealed record DisableMfaInput(Guid UserId, string Password, string Code);

public sealed class DisableMfaCommand(DisableMfaInput input)
    : CommandBase<DisableMfaInput, bool>(input);
