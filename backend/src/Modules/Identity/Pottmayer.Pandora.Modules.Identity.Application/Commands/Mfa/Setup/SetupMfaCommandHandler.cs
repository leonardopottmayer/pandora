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

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Setup;

public sealed class SetupMfaCommandHandler(
    IUnitOfWorkFactory factory,
    ITotpAuthenticator totp,
    ISecretProtector protector,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : CommandHandlerBase<SetupMfaCommand, MfaSetupDto>
{
    protected override async Task<Result<MfaSetupDto>> HandleAsync(SetupMfaCommand request, CancellationToken ct)
    {
        var userId = request.Input.UserId;
        var now = timeProvider.GetUtcNow();
        var secret = totp.GenerateSecret();
        var cipher = protector.Protect(secret);

        return await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(userId, token);
            if (user is null)
                return Fail(UserErrors.NotFound);

            if (user.MfaEnabled)
                return Fail(IdentityErrors.MfaAlreadyEnabled);

            // Replace any previous (unconfirmed) setup so a restart always uses a fresh secret.
            var credentials = ctx.AcquireRepository<IMfaCredentialRepository>();
            await credentials.RemoveByUserIdAsync(userId, token);
            await credentials.AddAsync(MfaCredential.Issue(userId, cipher, now), token);

            var otpauthUri = totp.BuildOtpauthUri(secret, options.Value.Issuer, user.Email.Value);
            return Ok(new MfaSetupDto(secret, otpauthUri));
        }, cancellationToken: ct);
    }
}
