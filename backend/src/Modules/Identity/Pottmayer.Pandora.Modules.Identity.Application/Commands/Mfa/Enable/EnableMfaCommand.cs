using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Enable;

public sealed record EnableMfaInput(Guid UserId, string Code);

public sealed class EnableMfaCommand(EnableMfaInput input)
    : CommandBase<EnableMfaInput, RecoveryCodesDto>(input);
