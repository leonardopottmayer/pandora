using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.RecoveryCodes;

public sealed record RegenerateRecoveryCodesInput(Guid UserId, string Password, string Code);

public sealed class RegenerateRecoveryCodesCommand(RegenerateRecoveryCodesInput input)
    : CommandBase<RegenerateRecoveryCodesInput, RecoveryCodesDto>(input);
