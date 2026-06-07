using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using RecoveryCodeFactory = Pottmayer.Pandora.Modules.Identity.Application.Security.RecoveryCodes;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.RecoveryCodes;

public sealed class RegenerateRecoveryCodesCommandHandler(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher,
    ITotpAuthenticator totp,
    ISecretProtector protector,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : CommandHandlerBase<RegenerateRecoveryCodesCommand, RecoveryCodesDto>
{
    protected override async Task<Result<RecoveryCodesDto>> HandleAsync(
        RegenerateRecoveryCodesCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var plaintextCodes = RecoveryCodeFactory.Generate(options.Value.RecoveryCodeCount);

        var result = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(input.UserId, token);
            if (user is null || !user.MfaEnabled)
                return Result<bool>.Failure([IdentityErrors.MfaNotEnabled]);

            if (!user.VerifyPassword(input.Password, passwordHasher))
                return Result<bool>.Failure([IdentityErrors.InvalidCredentials]);

            var credentials = ctx.AcquireRepository<IMfaCredentialRepository>();
            var recoveryCodes = ctx.AcquireRepository<IMfaRecoveryCodeRepository>();

            var verified = await MfaCodeVerifier.VerifyAsync(
                user.Id, input.Code, credentials, recoveryCodes, totp, protector, now, token);
            if (!verified)
                return Result<bool>.Failure([IdentityErrors.InvalidMfaCode]);

            await recoveryCodes.RemoveAllForUserAsync(user.Id, token);
            foreach (var code in plaintextCodes)
                await recoveryCodes.AddAsync(MfaRecoveryCode.Issue(user.Id, RecoveryCodeFactory.Hash(code), now), token);

            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsSuccess ? Ok(new RecoveryCodesDto(plaintextCodes)) : Fail([.. result.Errors]);
    }
}
