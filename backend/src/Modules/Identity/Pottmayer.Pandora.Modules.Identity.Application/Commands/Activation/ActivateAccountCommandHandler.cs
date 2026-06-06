using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;

public sealed class ActivateAccountCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<ActivateAccountCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(ActivateAccountCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input.Token))
            return Fail(IdentityErrors.InvalidActivationToken);

        var tokenHash = ActivationTokens.Hash(request.Input.Token);
        var now = timeProvider.GetUtcNow();

        return await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
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

            return Ok(true);
        }, cancellationToken: ct);
    }
}
