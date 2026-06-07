using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Application.Services;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Identity.Abstractions.Services;
using Pottmayer.Tars.Security.Identity.Abstractions.Token;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Challenge;

public sealed class CompleteMfaChallengeCommandHandler(
    IUnitOfWorkFactory factory,
    ITotpAuthenticator totp,
    ISecretProtector protector,
    ITokenIssuer tokenIssuer,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider)
    : CommandHandlerBase<CompleteMfaChallengeCommand, TokenDto>
{
    protected override async Task<Result<TokenDto>> HandleAsync(CompleteMfaChallengeCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var ticketHash = MfaTickets.Hash(input.Ticket);

        var authenticated = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var challenges = ctx.AcquireRepository<IMfaChallengeRepository>();
            var challenge = await challenges.FindByTokenHashAsync(ticketHash, token);
            if (challenge is null || !challenge.IsConsumable(now))
                return Result<Guid>.Failure([IdentityErrors.InvalidMfaChallenge]);

            var credentials = ctx.AcquireRepository<IMfaCredentialRepository>();
            var recoveryCodes = ctx.AcquireRepository<IMfaRecoveryCodeRepository>();

            var verified = await MfaCodeVerifier.VerifyAsync(
                challenge.UserId, input.Code, credentials, recoveryCodes, totp, protector, now, token);
            if (!verified)
                return Result<Guid>.Failure([IdentityErrors.InvalidMfaCode]);

            challenge.Consume(now);
            await challenges.UpdateAsync(challenge, token);

            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(challenge.UserId, token);
            if (user is null || !user.CanAuthenticate)
                return Result<Guid>.Failure([IdentityErrors.InvalidMfaChallenge]);

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
