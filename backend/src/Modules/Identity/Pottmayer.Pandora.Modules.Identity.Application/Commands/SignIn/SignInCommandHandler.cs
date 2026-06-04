using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Application.Services;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Identity.Abstractions.Services;
using Pottmayer.Tars.Security.Identity.Abstractions.Token;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;

public sealed class SignInCommandHandler(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider)
    : CommandHandlerBase<SignInCommand, TokenDto>
{
    protected override async Task<Result<TokenDto>> HandleAsync(SignInCommand request, CancellationToken ct)
    {
        var input = request.Input;

        // Verify credentials and stamp the sign-in within a single transaction.
        var authenticated = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();

            var user = Email.TryCreate(input.EmailOrUsername, out var email)
                ? await users.FindByEmailAsync(email!, token)
                : await users.FindByUsernameAsync(input.EmailOrUsername, token);

            if (user is null || !user.VerifyPassword(input.Password, passwordHasher))
                return Result<Guid>.Failure([IdentityErrors.InvalidCredentials]);

            if (!user.CanAuthenticate)
                return Result<Guid>.Failure([IdentityErrors.AccountNotActive]);

            user.RecordSuccessfulSignIn(timeProvider);
            await users.UpdateAsync(user, token);

            return Result<Guid>.Success(user.Id);
        }, cancellationToken: ct);

        if (authenticated.IsFailure)
            return Fail([.. authenticated.Errors]);

        var authResult = TokenMapper.ToAuthResult(authenticated.Value);
        var accessToken = await tokenIssuer.IssueAsync(authResult, ct);
        var refresh = await refreshTokenService.IssueAsync(authResult.Subject, authResult.Claims, null, ct);

        return Ok(new TokenDto(
            AccessToken: accessToken.AccessToken,
            AccessTokenExpiresAt: accessToken.ExpiresAt,
            RefreshToken: refresh.OpaqueToken,
            RefreshTokenExpiresAt: refresh.ExpiresAt));
    }
}
