using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Challenge;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Services;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
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
    IOptions<MfaOptions> mfaOptions,
    TimeProvider timeProvider)
    : CommandHandlerBase<SignInCommand, SignInResultDto>
{
    private sealed record SignInOutcome(Guid UserId, bool MfaRequired, string? Ticket, DateTimeOffset ChallengeExpiresAt);

    protected override async Task<Result<SignInResultDto>> HandleAsync(SignInCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        // Verify credentials within a single transaction. MFA accounts get a challenge ticket instead of
        // tokens and only have their sign-in stamped once the second factor is completed.
        var outcome = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();

            var user = Email.TryCreate(input.EmailOrUsername, out var email)
                ? await users.FindByEmailAsync(email!, token)
                : await users.FindByUsernameAsync(input.EmailOrUsername, token);

            if (user is null || !user.VerifyPassword(input.Password, passwordHasher))
                return Result<SignInOutcome>.Failure([IdentityErrors.InvalidCredentials]);

            if (!user.CanAuthenticate)
                return Result<SignInOutcome>.Failure([IdentityErrors.AccountNotActive]);

            if (user.MfaEnabled)
            {
                var ticket = MfaTickets.Generate();
                var challenge = MfaChallenge.Issue(
                    user.Id, MfaTickets.Hash(ticket), now + mfaOptions.Value.ChallengeLifetime);
                await ctx.AcquireRepository<IMfaChallengeRepository>().AddAsync(challenge, token);

                return Result<SignInOutcome>.Success(new SignInOutcome(user.Id, true, ticket, challenge.ExpiresAt));
            }

            user.RecordSuccessfulSignIn(timeProvider);
            await users.UpdateAsync(user, token);

            return Result<SignInOutcome>.Success(new SignInOutcome(user.Id, false, null, default));
        }, cancellationToken: ct);

        if (outcome.IsFailure)
            return Fail([.. outcome.Errors]);

        var value = outcome.Value;
        if (value.MfaRequired)
            return Ok(new SignInResultDto(null, new MfaChallengeDto(value.Ticket!, value.ChallengeExpiresAt)));

        var authResult = TokenMapper.ToAuthResult(value.UserId);
        var accessToken = await tokenIssuer.IssueAsync(authResult, ct);
        var refresh = await refreshTokenService.IssueAsync(authResult.Subject, authResult.Claims, null, ct);

        var tokens = new TokenDto(
            AccessToken: accessToken.AccessToken,
            AccessTokenExpiresAt: accessToken.ExpiresAt,
            RefreshToken: refresh.OpaqueToken,
            RefreshTokenExpiresAt: refresh.ExpiresAt);

        return Ok(new SignInResultDto(tokens, null));
    }
}
