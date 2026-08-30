using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Oauth;
using Pottmayer.Pandora.Modules.Integrations.Contracts;
using Pottmayer.Pandora.Modules.Integrations.Domain.Errors;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;
using Pottmayer.Tars.Security.DataProtection.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.DisconnectAccount;

/// <summary>
/// Revokes the connection at the provider (best effort), deletes it locally, and announces the fact
/// so consumers can disable the bindings that used it — while leaving already-synced data in place.
/// </summary>
public sealed class DisconnectAccountCommandHandler(
    IUnitOfWorkFactory factory,
    OAuthProviderRegistry registry,
    ISecretProtector protector,
    IIntegrationEventBus bus,
    TimeProvider timeProvider)
    : CommandHandlerBase<DisconnectAccountCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DisconnectAccountCommand request, CancellationToken ct)
    {
        var input = request.Input;

        // Load and check ownership before touching the provider.
        var account = await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            var found = await repo.GetByIdAsync(input.AccountId, token);
            return found is not null && found.UserId == input.UserId ? found : null;
        }, cancellationToken: ct);

        if (account is null)
            return Fail(IntegrationErrors.AccountNotFound);

        // Best-effort revoke at the provider — a failure here must not block local deletion.
        if (registry.TryGet(account.Provider, out var provider))
        {
            var tokenToRevoke = account.RefreshTokenEnc ?? account.AccessTokenEnc;
            if (tokenToRevoke is not null)
            {
                try
                {
                    await provider.RevokeAsync(protector.Unprotect(tokenToRevoke), ct);
                }
                catch
                {
                    // Swallowed on purpose: the local connection is going away regardless.
                }
            }
        }

        await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            var fresh = await repo.GetByIdAsync(input.AccountId, token);
            if (fresh is not null)
                await repo.RemoveAsync(fresh, token);

            // Published inside the unit of work: the local deletion and its announcement commit
            // together (transactional outbox).
            await bus.PublishAsync(
                new ExternalAccountDisconnected(
                    Guid.CreateVersion7(), timeProvider.GetUtcNow(), account.UserId, account.Id, account.Provider),
                token);
            return true;
        }, cancellationToken: ct);

        return Ok(true);
    }
}
