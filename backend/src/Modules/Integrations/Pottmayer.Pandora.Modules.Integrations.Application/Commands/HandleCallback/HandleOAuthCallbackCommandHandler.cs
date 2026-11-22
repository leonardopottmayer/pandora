using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Oauth;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.Errors;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.DataProtection.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.HandleCallback;

/// <summary>
/// Validates and consumes the state, exchanges the code for tokens, and upserts the account the
/// provider returned — matched by its provider identity (the Google <c>sub</c>), so a new identity is a
/// new account and a known one is reconnected (e.g. a widened-scope re-consent).
/// </summary>
/// <remarks>
/// When the flow targeted an account but the user signed in with a different identity, the target is
/// left untouched — a credential for one person is never written onto another's account — and the
/// redirect carries <c>integration=other-account</c> so the screen can say what happened.
/// </remarks>
public sealed class HandleOAuthCallbackCommandHandler(
    IUnitOfWorkFactory factory,
    OAuthProviderRegistry registry,
    ISecretProtector protector,
    TimeProvider timeProvider)
    : CommandHandlerBase<HandleOAuthCallbackCommand, string>
{
    /// <summary>Query flag added to the redirect when the returned identity is not the targeted account's.</summary>
    public const string OtherAccountOutcome = "other-account";

    protected override async Task<Result<string>> HandleAsync(HandleOAuthCallbackCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!registry.TryGet(input.Provider, out var provider))
            return Fail(IntegrationErrors.UnknownProvider(input.Provider));

        // Consume the state up front: it is single-use, and this is the CSRF check.
        var consumed = await ConsumeStateAsync(input.State, provider.Name, ct);
        if (consumed is null)
            return Fail(IntegrationErrors.StateInvalid);

        var verifier = protector.Unprotect(consumed.CodeVerifierEnc);

        OAuthTokens tokens;
        try
        {
            tokens = await provider.ExchangeCodeAsync(input.Code, verifier, ct);
        }
        catch (OAuthException)
        {
            return Fail(IntegrationErrors.StateInvalid);
        }

        var providerAccountId = tokens.ProviderAccountId ?? consumed.UserId.ToString();
        var scopes = ScopeString.Join(tokens.Scopes);
        var accessEnc = protector.Protect(tokens.AccessToken);
        var refreshEnc = tokens.RefreshToken is null ? null : protector.Protect(tokens.RefreshToken);

        var landedElsewhere = await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var accounts = context.AcquireRepository<IExternalAccountRepository>();
            var log = context.AcquireRepository<IIntegrationEventLogRepository>();
            var existing = await accounts.FindAsync(consumed.UserId, provider.Name, providerAccountId, token);

            if (existing is null)
            {
                var account = ExternalAccount.ConnectOAuth(
                    consumed.UserId, provider.Name, providerAccountId,
                    tokens.DisplayName, scopes, accessEnc, tokens.ExpiresAt, refreshEnc, timeProvider);
                await accounts.AddAsync(account, token);
                await log.AddAsync(IntegrationEventLogEntry.Record(
                    consumed.UserId, account.Id, provider.Name, IntegrationEventType.Connected, null, timeProvider), token);
                return consumed.TargetAccountId is not null;
            }

            existing.ReconnectOAuth(tokens.DisplayName, scopes, accessEnc, tokens.ExpiresAt, refreshEnc, timeProvider);
            await accounts.UpdateAsync(existing, token);
            await log.AddAsync(IntegrationEventLogEntry.Record(
                consumed.UserId, existing.Id, provider.Name, IntegrationEventType.Reconnected, null, timeProvider), token);
            return consumed.TargetAccountId is { } target && target != existing.Id;
        }, cancellationToken: ct);

        return Ok(landedElsewhere ? WithOutcome(consumed.RedirectAfter, OtherAccountOutcome) : consumed.RedirectAfter);
    }

    private static string WithOutcome(string path, string outcome) =>
        $"{path}{(path.Contains('?') ? '&' : '?')}integration={outcome}";

    private Task<OAuthState?> ConsumeStateAsync(string state, string provider, CancellationToken ct) =>
        factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var states = context.AcquireRepository<IOAuthStateRepository>();
            var pending = await states.FindByStateAsync(state, token);
            if (pending is null || pending.Provider != provider || !pending.IsUsable(timeProvider.GetUtcNow()))
                return null;

            pending.Consume(timeProvider);
            await states.UpdateAsync(pending, token);
            return pending;
        }, cancellationToken: ct);
}
