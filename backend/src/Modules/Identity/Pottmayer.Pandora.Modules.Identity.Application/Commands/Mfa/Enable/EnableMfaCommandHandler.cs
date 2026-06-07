using System.Globalization;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;
using RecoveryCodeFactory = Pottmayer.Pandora.Modules.Identity.Application.Security.RecoveryCodes;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Enable;

public sealed class EnableMfaCommandHandler(
    IUnitOfWorkFactory factory,
    ITotpAuthenticator totp,
    ISecretProtector protector,
    IIntegrationEventBus integrationEventBus,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : CommandHandlerBase<EnableMfaCommand, RecoveryCodesDto>
{
    protected override async Task<Result<RecoveryCodesDto>> HandleAsync(EnableMfaCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        // Recovery codes: keep plaintext to return, persist only the hashes.
        var plaintextCodes = RecoveryCodeFactory.Generate(options.Value.RecoveryCodeCount);

        var result = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(input.UserId, token);
            if (user is null)
                return Result<string>.Failure([UserErrors.NotFound]);

            if (user.MfaEnabled)
                return Result<string>.Failure([IdentityErrors.MfaAlreadyEnabled]);

            var credentials = ctx.AcquireRepository<IMfaCredentialRepository>();
            var credential = await credentials.FindByUserIdAsync(input.UserId, token);
            if (credential is null || credential.IsConfirmed)
                return Result<string>.Failure([IdentityErrors.MfaSetupNotFound]);

            // Enabling requires a genuine TOTP code (recovery codes don't exist yet).
            if (!totp.VerifyCode(protector.Unprotect(credential.SecretCipher), input.Code))
                return Result<string>.Failure([IdentityErrors.InvalidMfaCode]);

            credential.Confirm(now);
            await credentials.UpdateAsync(credential, token);

            user.EnableMfa();
            await users.UpdateAsync(user, token);

            var recoveryCodes = ctx.AcquireRepository<IMfaRecoveryCodeRepository>();
            foreach (var code in plaintextCodes)
                await recoveryCodes.AddAsync(MfaRecoveryCode.Issue(user.Id, RecoveryCodeFactory.Hash(code), now), token);

            return Result<string>.Success(user.Email.Value);
        }, cancellationToken: ct);

        if (result.IsFailure)
            return Fail([.. result.Errors]);

        var enabled = new MfaEnabled(
            EventId: Guid.CreateVersion7(),
            OccurredAt: now,
            UserId: input.UserId,
            Email: result.Value,
            Locale: CultureInfo.CurrentUICulture.Name);
        await integrationEventBus.PublishAsync(enabled, ct);

        return Ok(new RecoveryCodesDto(plaintextCodes));
    }
}
