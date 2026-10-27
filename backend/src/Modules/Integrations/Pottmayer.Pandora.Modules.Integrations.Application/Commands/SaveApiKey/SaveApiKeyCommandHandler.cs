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

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.SaveApiKey;

/// <summary>
/// Protects the API key and stores it as a new <c>api-key</c> <see cref="ExternalAccount"/>, recording
/// the connection in the event log so the connection health view mirrors the OAuth path.
/// </summary>
public sealed class SaveApiKeyCommandHandler(
    IUnitOfWorkFactory factory,
    CredentialProviderRegistry registry,
    ISecretProtector protector,
    TimeProvider timeProvider)
    : CommandHandlerBase<SaveApiKeyCommand, Guid>
{
    protected override async Task<Result<Guid>> HandleAsync(SaveApiKeyCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!registry.TryGet(input.Provider, AuthKind.ApiKey, out var provider))
            return Fail(IntegrationErrors.UnknownProvider(input.Provider));

        var apiKey = input.ApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
            return Fail(IntegrationErrors.ApiKeyRequired);

        var mask = SecretMask.Of(apiKey);
        var label = string.IsNullOrWhiteSpace(input.Label) ? $"{provider.DisplayName} {mask}" : input.Label;

        var account = ExternalAccount.ConnectApiKey(
            input.UserId, provider.Name, protector.Protect(apiKey), mask, label, timeProvider);

        await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            await context.AcquireRepository<IExternalAccountRepository>().AddAsync(account, token);
            await context.AcquireRepository<IIntegrationEventLogRepository>().AddAsync(IntegrationEventLogEntry.Record(
                input.UserId, account.Id, provider.Name, IntegrationEventType.Connected, null, timeProvider), token);
            return true;
        }, cancellationToken: ct);

        return Ok(account.Id);
    }
}
