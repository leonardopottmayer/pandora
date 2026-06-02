using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.RefreshToken;

public sealed record RefreshTokenInput(string RefreshToken);

public sealed class RefreshTokenCommand(RefreshTokenInput input)
    : CommandBase<RefreshTokenInput, TokenDto>(input);
