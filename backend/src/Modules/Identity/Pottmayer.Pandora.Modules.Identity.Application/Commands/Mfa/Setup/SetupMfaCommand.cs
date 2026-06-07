using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Setup;

public sealed record SetupMfaInput(Guid UserId);

public sealed class SetupMfaCommand(SetupMfaInput input)
    : CommandBase<SetupMfaInput, MfaSetupDto>(input);
