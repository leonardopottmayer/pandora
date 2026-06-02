using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Application.Services;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Users.Contracts.Authentication;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Security.Identity.Abstractions.Services;
using Pottmayer.Tars.Security.Identity.Abstractions.Token;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;

public sealed class SignInCommandHandler(
    IUserAuthenticator users,
    ITokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokenService)
    : CommandHandlerBase<SignInCommand, TokenDto>
{
    protected override async Task<Result<TokenDto>> HandleAsync(SignInCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var auth  = await users.AuthenticateAsync(input.EmailOrUsername, input.Password, ct);

        if (auth.Status == UserAuthStatus.AccountNotActive)
            return Fail(IdentityErrors.AccountNotActive);

        if (!auth.IsSuccess)
            return Fail(IdentityErrors.InvalidCredentials);

        var authResult  = TokenMapper.ToAuthResult(auth.User!);
        var accessToken = await tokenIssuer.IssueAsync(authResult, ct);
        var refresh     = await refreshTokenService.IssueAsync(authResult.Subject, authResult.Claims, null, ct);

        return Ok(new TokenDto(
            AccessToken:           accessToken.AccessToken,
            AccessTokenExpiresAt:  accessToken.ExpiresAt,
            RefreshToken:          refresh.OpaqueToken,
            RefreshTokenExpiresAt: refresh.ExpiresAt));
    }
}
