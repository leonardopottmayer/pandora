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

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    IUnitOfWorkFactory factory,
    IPasswordHasher passwordHasher,
    IIntegrationEventBus integrationEventBus,
    TimeProvider timeProvider)
    : CommandHandlerBase<ChangePasswordCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(ChangePasswordCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!PasswordPolicy.IsSatisfiedBy(input.NewPassword))
            return Fail(IdentityErrors.WeakPassword);

        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(input.UserId, token);

            // Wrong current password is reported as invalid credentials (no user enumeration).
            if (user is null || !user.VerifyPassword(input.CurrentPassword, passwordHasher))
                return Result<string>.Failure([IdentityErrors.InvalidCredentials]);

            user.ChangePassword(passwordHasher.Hash(input.NewPassword), timeProvider);
            await users.UpdateAsync(user, token);

            // Revoke every active session so other devices must re-authenticate.
            await ctx.AcquireRepository<IRefreshTokenRepository>()
                     .RevokeAllForSubjectAsync(user.Id.ToString(), token);

            return Result<string>.Success(user.Email.Value);
        }, cancellationToken: ct);

        if (result.IsSuccess)
        {
            var changed = new PasswordChanged(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                UserId: input.UserId,
                Email: result.Value,
                Locale: CultureInfo.CurrentUICulture.Name);

            await integrationEventBus.PublishAsync(changed, ct);
        }

        return result.IsSuccess ? Ok(true) : Fail([.. result.Errors]);
    }
}
