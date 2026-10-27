using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Credentials;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.Errors;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.DataProtection.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.ReplaceSecret;

/// <summary>
/// Protects the new secret and swaps it into the account, logging a reconnect. An API key's masked hint
/// follows the new key; a password account keeps its username as the hint.
/// </summary>
public sealed class ReplaceSecretCommandHandler(
    IUnitOfWorkFactory factory,
    ISecretProtector protector,
    TimeProvider timeProvider)
    : CommandHandlerBase<ReplaceSecretCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(ReplaceSecretCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            var account = await repo.GetOwnedAsync(input.UserId, input.AccountId, token);
            if (account is null)
                return Result<bool>.Failure(IntegrationErrors.AccountNotFound);
            if (account.AuthKind == AuthKind.OAuth)
                return Result<bool>.Failure(IntegrationErrors.SecretNotReplaceable);

            var isApiKey = account.AuthKind == AuthKind.ApiKey;
            var secret = isApiKey ? input.Secret?.Trim() : input.Secret;
            if (string.IsNullOrEmpty(secret))
                return Result<bool>.Failure(isApiKey ? IntegrationErrors.ApiKeyRequired : IntegrationErrors.PasswordRequired);

            account.ReplaceSecret(protector.Protect(secret), isApiKey ? SecretMask.Of(secret) : null, timeProvider);
            await repo.UpdateAsync(account, token);
            await context.AcquireRepository<IIntegrationEventLogRepository>().AddAsync(IntegrationEventLogEntry.Record(
                account.UserId, account.Id, account.Provider, IntegrationEventType.Reconnected, null, timeProvider), token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result;
    }
}
