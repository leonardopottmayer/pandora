using System.Globalization;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
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
    TimeProvider timeProvider)
    : CommandHandlerBase<ActivateAccountCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(ActivateAccountCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input.Token))
            return Fail(IdentityErrors.InvalidActivationToken);

        var tokenHash = ActivationTokens.Hash(request.Input.Token);
        var now = timeProvider.GetUtcNow();

        Guid activatedUserId = default;
        string? activatedEmail = null;

        var result = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var tokens = ctx.AcquireRepository<IActivationTokenRepository>();
            var activation = await tokens.FindByTokenHashAsync(tokenHash, token);

            if (activation is null || !activation.IsConsumable(now))
                return Fail(IdentityErrors.InvalidActivationToken);

            var users = ctx.AcquireRepository<IUserRepository>();
            var user = await users.GetByIdAsync(activation.UserId, token);
            if (user is null)
                return Fail(IdentityErrors.InvalidActivationToken);

            user.ConfirmEmail(timeProvider);
            await users.UpdateAsync(user, token);

            activation.Consume(now);
            await tokens.UpdateAsync(activation, token);

            activatedUserId = user.Id;
            activatedEmail = user.Email.Value;

            return Ok(true);
        }, cancellationToken: ct);

        // After commit: activation proves the user owns the e-mail, so ask Channels to provision the
        // verified e-mail channel (in-process; broker-ready). Only fires on a genuine activation — a
        // spent token is refused above and never reaches here.
        if (result.IsSuccess && activatedEmail is not null)
        {
            var activated = new AccountActivated(
                EventId: Guid.CreateVersion7(),
                OccurredAt: now,
                UserId: activatedUserId,
                Email: activatedEmail,
                Locale: CultureInfo.CurrentUICulture.Name);

            await integrationEventBus.PublishAsync(activated, ct);
        }

        return result;
    }
}
