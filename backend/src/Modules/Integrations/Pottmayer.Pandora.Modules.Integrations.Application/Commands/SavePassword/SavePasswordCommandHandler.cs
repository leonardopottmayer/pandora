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

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.SavePassword;

/// <summary>
/// Protects the password and upserts the <c>password</c> <see cref="ExternalAccount"/> on
/// (user, provider, username), recording connected/reconnected in the event log.
/// </summary>
public sealed class SavePasswordCommandHandler(
    IUnitOfWorkFactory factory,
    CredentialProviderRegistry registry,
    ISecretProtector protector,
    TimeProvider timeProvider)
    : CommandHandlerBase<SavePasswordCommand, Guid>
{
    protected override async Task<Result<Guid>> HandleAsync(SavePasswordCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!registry.TryGet(input.Provider, AuthKind.Password, out var provider))
            return Fail(IntegrationErrors.UnknownProvider(input.Provider));

        var username = input.Username?.Trim();
        if (string.IsNullOrEmpty(username))
            return Fail(IntegrationErrors.UsernameRequired);

        // Not trimmed: spaces can be part of a password.
        if (string.IsNullOrEmpty(input.Password))
            return Fail(IntegrationErrors.PasswordRequired);

        var passwordEnc = protector.Protect(input.Password);

        var accountId = await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            var log = context.AcquireRepository<IIntegrationEventLogRepository>();

            var existing = await repo.FindAsync(input.UserId, provider.Name, username, token);
            if (existing is null)
            {
                var account = ExternalAccount.ConnectPassword(
                    input.UserId, provider.Name, username, passwordEnc, input.Label, timeProvider);
                await repo.AddAsync(account, token);
                await log.AddAsync(IntegrationEventLogEntry.Record(
                    input.UserId, account.Id, provider.Name, IntegrationEventType.Connected, null, timeProvider), token);
                return account.Id;
            }

            existing.ReplaceSecret(passwordEnc, displayName: null, timeProvider);
            await repo.UpdateAsync(existing, token);
            await log.AddAsync(IntegrationEventLogEntry.Record(
                input.UserId, existing.Id, provider.Name, IntegrationEventType.Reconnected, null, timeProvider), token);
            return existing.Id;
        }, cancellationToken: ct);

        return Ok(accountId);
    }
}
