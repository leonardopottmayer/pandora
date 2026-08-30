using System.Globalization;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.PasswordReset;

public sealed class RequestPasswordResetCommandHandler(
    IUnitOfWorkFactory factory,
    IIntegrationEventBus integrationEventBus,
    IOptions<PasswordResetOptions> resetOptions,
    TimeProvider timeProvider)
    : CommandHandlerBase<RequestPasswordResetCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(RequestPasswordResetCommand request, CancellationToken ct)
    {
        // Always succeed, even for unknown e-mails: never reveal whether an account exists.
        if (!Email.TryCreate(request.Input.Email, out var email))
            return Ok(true);

        var now = timeProvider.GetUtcNow();
        var resetToken = PasswordResetTokens.Generate();

        _ = await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.FindByEmailAsync(email!, token);

            // Only confirmed, enabled accounts can reset. Silently no-op otherwise.
            if (user is null || !user.CanAuthenticate)
                return (Guid?)null;

            var reset = PasswordResetToken.Issue(
                user.Id,
                PasswordResetTokens.Hash(resetToken),
                now + resetOptions.Value.TokenLifetime);
            await ctx.AcquireRepository<IPasswordResetTokenRepository>().AddAsync(reset, token);

            // Published inside the unit of work: the reset token and its e-mail request commit
            // together (transactional outbox).
            var resetRequested = new PasswordResetRequested(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                UserId: user.Id,
                Email: email!.Value,
                Token: resetToken,
                Locale: CultureInfo.CurrentUICulture.Name);
            await integrationEventBus.PublishAsync(resetRequested, token);

            return user.Id;
        }, cancellationToken: ct);

        return Ok(true);
    }
}
