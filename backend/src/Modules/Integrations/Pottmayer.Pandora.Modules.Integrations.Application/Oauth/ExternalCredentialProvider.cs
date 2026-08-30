using System.Collections.Concurrent;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Integrations.Contracts;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.Errors;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;
using Pottmayer.Tars.Security.DataProtection.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Oauth;

/// <summary>
/// The synchronous port every consumer uses. Returns a valid access token, refreshing invisibly, and
/// never lets a refresh token leave the module.
/// </summary>
/// <remarks>
/// Refresh is serialized per account by an in-process gate rather than a Postgres advisory lock.
/// Pandora is a single in-process monolith (the same assumption the Channels long-poll makes), so a
/// per-account <see cref="SemaphoreSlim"/> is enough to stop two concurrent sync jobs burning two
/// refreshes — which matters because some providers rotate the refresh token on use. A multi-instance
/// deployment would need to revisit this.
/// </remarks>
public sealed class ExternalCredentialProvider(
    IUnitOfWorkFactory factory,
    OAuthProviderRegistry registry,
    ISecretProtector protector,
    IIntegrationEventBus bus,
    TimeProvider timeProvider) : IExternalCredentialProvider
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    public async Task<Result<ExternalAccessToken>> GetAccessTokenAsync(
        Guid userId, string provider, CancellationToken ct = default)
    {
        var account = await LoadAsync(userId, provider, ct);
        if (account is null)
            return Result<ExternalAccessToken>.Failure(IntegrationErrors.NotConnected(provider));
        if (account.AuthKind == AuthKind.ApiKey)
            return Result<ExternalAccessToken>.Failure(IntegrationErrors.NotConnected(provider));
        if (account.Status == AccountStatus.Revoked)
            return Result<ExternalAccessToken>.Failure(IntegrationErrors.AccountRevoked);

        if (!account.NeedsRefresh(timeProvider.GetUtcNow(), RefreshMargin))
            return Result<ExternalAccessToken>.Success(ToToken(account));

        var gate = Gates.GetOrAdd(account.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            return await RefreshAndReturnAsync(userId, provider, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<string>> GetApiKeyAsync(Guid userId, string provider, CancellationToken ct = default)
    {
        var account = await LoadAsync(userId, provider, ct);
        if (account is null)
            return Result<string>.Failure(IntegrationErrors.NotConnected(provider));
        if (account.AuthKind != AuthKind.ApiKey || account.AccessTokenEnc is null)
            return Result<string>.Failure(IntegrationErrors.NotAnApiKey(provider));

        return Result<string>.Success(protector.Unprotect(account.AccessTokenEnc));
    }

    private async Task<Result<ExternalAccessToken>> RefreshAndReturnAsync(
        Guid userId, string provider, CancellationToken ct)
    {
        // Re-read under the gate: an earlier waiter may already have refreshed.
        var account = await LoadAsync(userId, provider, ct);
        if (account is null)
            return Result<ExternalAccessToken>.Failure(IntegrationErrors.NotConnected(provider));
        if (account.Status == AccountStatus.Revoked)
            return Result<ExternalAccessToken>.Failure(IntegrationErrors.AccountRevoked);
        if (!account.NeedsRefresh(timeProvider.GetUtcNow(), RefreshMargin))
            return Result<ExternalAccessToken>.Success(ToToken(account));

        if (account.RefreshTokenEnc is null)
        {
            await PersistAsync(account.Id, a => a.MarkExpired(), ct);
            return Result<ExternalAccessToken>.Failure(IntegrationErrors.NoRefreshToken);
        }

        if (!registry.TryGet(provider, out var oauth))
            return Result<ExternalAccessToken>.Failure(IntegrationErrors.UnknownProvider(provider));

        OAuthTokens tokens;
        try
        {
            // The provider call is deliberately outside any database transaction.
            var refreshToken = protector.Unprotect(account.RefreshTokenEnc);
            tokens = await oauth.RefreshAsync(refreshToken, ct);
        }
        catch (OAuthException ex) when (ex.IsPermanent)
        {
            // Mark revoked and announce it in one transaction (transactional outbox): the
            // revocation and the event that disables its bindings can never disagree.
            await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
            {
                var repo = context.AcquireRepository<IExternalAccountRepository>();
                var fresh = await repo.GetByIdAsync(account.Id, token);
                if (fresh is null)
                    return false;

                fresh.MarkRevoked(ex.Message);
                await repo.UpdateAsync(fresh, token);
                await bus.PublishAsync(
                    new ExternalAccountRevoked(
                        Guid.CreateVersion7(), timeProvider.GetUtcNow(), userId, account.Id, provider),
                    token);
                return true;
            }, cancellationToken: ct);

            return Result<ExternalAccessToken>.Failure(IntegrationErrors.AccountRevoked);
        }
        catch (OAuthException)
        {
            return Result<ExternalAccessToken>.Failure(IntegrationErrors.RefreshFailed);
        }

        var accessEnc = protector.Protect(tokens.AccessToken);
        var refreshEnc = tokens.RefreshToken is null ? null : protector.Protect(tokens.RefreshToken);

        ExternalAccessToken? issued = null;
        await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            var fresh = await repo.GetByIdAsync(account.Id, token);
            if (fresh is null)
                return false;

            fresh.ApplyRefreshedTokens(accessEnc, tokens.ExpiresAt, refreshEnc, timeProvider);
            await repo.UpdateAsync(fresh, token);
            issued = new ExternalAccessToken(tokens.AccessToken, tokens.ExpiresAt, ScopeString.Split(fresh.Scopes));
            return true;
        }, cancellationToken: ct);

        return issued is null
            ? Result<ExternalAccessToken>.Failure(IntegrationErrors.NotConnected(provider))
            : Result<ExternalAccessToken>.Success(issued);
    }

    private Task<ExternalAccount?> LoadAsync(Guid userId, string provider, CancellationToken ct) =>
        factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            return await repo.FindAsync(userId, provider, token);
        }, cancellationToken: ct);

    private Task PersistAsync(Guid accountId, Action<ExternalAccount> mutate, CancellationToken ct) =>
        factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IExternalAccountRepository>();
            var account = await repo.GetByIdAsync(accountId, token);
            if (account is null)
                return false;

            mutate(account);
            await repo.UpdateAsync(account, token);
            return true;
        }, cancellationToken: ct);

    private ExternalAccessToken ToToken(ExternalAccount account) =>
        new(protector.Unprotect(account.AccessTokenEnc!),
            account.AccessTokenExpiresAt!.Value,
            ScopeString.Split(account.Scopes));
}
