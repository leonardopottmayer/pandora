using System.Globalization;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Disable;

public sealed class DisableMfaCommandHandler(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher,
    ITotpAuthenticator totp,
    ISecretProtector protector,
    IIntegrationEventBus integrationEventBus,
    TimeProvider timeProvider)
    : CommandHandlerBase<DisableMfaCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DisableMfaCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(input.UserId, token);
            if (user is null || !user.MfaEnabled)
                return Result<string>.Failure([IdentityErrors.MfaNotEnabled]);

            // Wrong password is reported as invalid credentials (no enumeration).
            if (!user.VerifyPassword(input.Password, passwordHasher))
                return Result<string>.Failure([IdentityErrors.InvalidCredentials]);

            var credentials = ctx.AcquireRepository<IMfaCredentialRepository>();
            var recoveryCodes = ctx.AcquireRepository<IMfaRecoveryCodeRepository>();

            var verified = await MfaCodeVerifier.VerifyAsync(
                user.Id, input.Code, credentials, recoveryCodes, totp, protector, now, token);
            if (!verified)
                return Result<string>.Failure([IdentityErrors.InvalidMfaCode]);

            user.DisableMfa();
            await users.UpdateAsync(user, token);

            await credentials.RemoveByUserIdAsync(user.Id, token);
            await recoveryCodes.RemoveAllForUserAsync(user.Id, token);

            return Result<string>.Success(user.Email.Value);
        }, cancellationToken: ct);

        if (result.IsFailure)
            return Fail([.. result.Errors]);

        var disabled = new MfaDisabled(
            EventId: Guid.CreateVersion7(),
            OccurredAt: now,
            UserId: input.UserId,
            Email: result.Value,
            Locale: CultureInfo.CurrentUICulture.Name);
        await integrationEventBus.PublishAsync(disabled, ct);

        return Ok(true);
    }
}
