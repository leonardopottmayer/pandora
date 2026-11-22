using System.Globalization;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Services;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;

public sealed class ActivateAccountCommandHandler(
    IUnitOfWorkFactory factory,
    IIntegrationEventBus integrationEventBus,
    AccountApprovalRule approvalRule,
    TimeProvider timeProvider)
    : CommandHandlerBase<ActivateAccountCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(ActivateAccountCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input.Token))
            return Fail(IdentityErrors.InvalidActivationToken);

        var tokenHash = ActivationTokens.Hash(request.Input.Token);
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            var tokens = ctx.AcquireRepository<IActivationTokenRepository>();
            var activation = await tokens.FindByTokenHashAsync(tokenHash, token);

            if (activation is null || !activation.IsConsumable(now))
                return Fail(IdentityErrors.InvalidActivationToken);

            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(activation.UserId, token);
            if (user is null)
                return Fail(IdentityErrors.InvalidActivationToken);

            // Decided now, not at every sign-in: accounts activated while the option was off never wait.
            var mustWait = user.EmailConfirmedAt is null && approvalRule.Applies(user.Email.Value);
            user.ConfirmEmail(timeProvider);
            if (mustWait)
                user.AwaitApproval(timeProvider);
            await users.UpdateAsync(user, token);

            activation.Consume(now);
            await tokens.UpdateAsync(activation, token);

            // Activation proves the user owns the e-mail, so ask Channels to provision the verified
            // e-mail channel. Published inside the unit of work: the outbox row commits with the
            // activation, so the two can never disagree (transactional outbox).
            var activated = new AccountActivated(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                UserId: user.Id,
                Email: user.Email.Value,
                Locale: CultureInfo.CurrentUICulture.Name);
            await integrationEventBus.PublishAsync(activated, token);

            if (mustWait)
            {
                await integrationEventBus.PublishAsync(new AccountApprovalRequested(
                    EventId: Guid.CreateVersion7(),
                    OccurredAt: now,
                    UserId: user.Id,
                    Name: user.Name,
                    Email: user.Email.Value,
                    ApproverEmails: approvalRule.ApproverEmails(),
                    Locale: CultureInfo.CurrentUICulture.Name), token);
            }

            return Ok(true);
        }, cancellationToken: ct);

        return result;
    }
}
