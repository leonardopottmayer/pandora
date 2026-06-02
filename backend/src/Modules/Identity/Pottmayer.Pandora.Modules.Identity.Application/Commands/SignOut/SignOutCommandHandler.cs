using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Security.Identity.Abstractions.Stores;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.SignOut;

public sealed class SignOutCommandHandler(IRefreshTokenStore refreshTokenStore)
    : CommandHandlerBase<SignOutCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(SignOutCommand request, CancellationToken ct)
    {
        await refreshTokenStore.RevokeAsync(request.Input.RefreshToken, ct);
        return Ok(true);
    }
}
