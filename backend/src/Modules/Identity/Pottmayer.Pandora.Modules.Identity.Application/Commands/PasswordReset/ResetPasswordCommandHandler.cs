using System.Globalization;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Security;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.PasswordReset;

public sealed class ResetPasswordCommandHandler(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher,
    IIntegrationEventBus integrationEventBus,
    TimeProvider timeProvider)
    : CommandHandlerBase<ResetPasswordCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(ResetPasswordCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Token))
            return Fail(IdentityErrors.InvalidPasswordResetToken);

        if (!PasswordPolicy.IsSatisfiedBy(input.NewPassword))
            return Fail(IdentityErrors.WeakPassword);

        var tokenHash = PasswordResetTokens.Hash(input.Token);
        var now = timeProvider.GetUtcNow();
        var passwordHash = passwordHasher.Hash(input.NewPassword);

        var result = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var resets = ctx.AcquireRepository<IPasswordResetTokenRepository>();
            var reset = await resets.FindByTokenHashAsync(tokenHash, token);

            if (reset is null || !reset.IsConsumable(now))
                return Result<(Guid UserId, string Email)>.Failure([IdentityErrors.InvalidPasswordResetToken]);

            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(reset.UserId, token);
            if (user is null)
                return Result<(Guid UserId, string Email)>.Failure([IdentityErrors.InvalidPasswordResetToken]);

            user.ChangePassword(passwordHash, timeProvider);
            await users.UpdateAsync(user, token);

            reset.Consume(now);
            await resets.UpdateAsync(reset, token);

            // Revoke every active session: a reset implies the old credentials are compromised.
            await ctx.AcquireRepository<IRefreshTokenRepository>()
                     .RevokeAllForSubjectAsync(user.Id.ToString(), token);

            return Result<(Guid UserId, string Email)>.Success((user.Id, user.Email.Value));
        }, cancellationToken: ct);

        if (result.IsSuccess)
        {
            var changed = new PasswordChanged(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                UserId: result.Value.UserId,
                Email: result.Value.Email,
                Locale: CultureInfo.CurrentUICulture.Name);

            await integrationEventBus.PublishAsync(changed, ct);
        }

        return result.IsSuccess ? Ok(true) : Fail([.. result.Errors]);
    }
}
