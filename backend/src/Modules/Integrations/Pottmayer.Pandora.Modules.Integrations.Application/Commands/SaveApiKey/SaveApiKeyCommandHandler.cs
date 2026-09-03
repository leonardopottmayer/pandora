using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.ApiKeys;
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
/// Protects and stores the API key as an <c>api_key</c> <see cref="ExternalAccount"/>, upserting on
/// (user, provider): a first save connects it, a later save replaces the key. Records a lifecycle entry
/// in the event log so the connection health view mirrors the OAuth path.
/// </summary>
public sealed class SaveApiKeyCommandHandler(
    IUnitOfWorkFactory factory,
    ApiKeyProviderRegistry registry,
    ISecretProtector protector,
    TimeProvider timeProvider)
    : CommandHandlerBase<SaveApiKeyCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(SaveApiKeyCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!registry.TryGet(input.Provider, out var provider))
            return Fail(IntegrationErrors.UnknownProvider(input.Provider));

        var apiKey = input.ApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
            return Fail(IntegrationErrors.ApiKeyRequired);

        var apiKeyEnc = protector.Protect(apiKey);
        var displayName = Mask(apiKey);

        await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            var log = context.AcquireRepository<IIntegrationEventLogRepository>();

            var existing = await repo.FindAsync(input.UserId, provider.Name, token);
            if (existing is null)
            {
                var account = ExternalAccount.ConnectApiKey(
                    input.UserId, provider.Name, apiKeyEnc, displayName, timeProvider);
                await repo.AddAsync(account, token);
                await log.AddAsync(IntegrationEventLogEntry.Record(
                    input.UserId, account.Id, provider.Name, IntegrationEventType.Connected, null, timeProvider), token);
            }
            else
            {
                existing.ReplaceApiKey(apiKeyEnc, displayName, timeProvider);
                await repo.UpdateAsync(existing, token);
                await log.AddAsync(IntegrationEventLogEntry.Record(
                    input.UserId, existing.Id, provider.Name, IntegrationEventType.Reconnected, null, timeProvider), token);
            }

            return true;
        }, cancellationToken: ct);

        return Ok(true);
    }

    /// <summary>A non-secret hint for settings: bullets plus the last four characters.</summary>
    private static string Mask(string apiKey)
    {
        const string bullets = "••••";
        return apiKey.Length <= 4 ? bullets : bullets + apiKey[^4..];
    }
}
