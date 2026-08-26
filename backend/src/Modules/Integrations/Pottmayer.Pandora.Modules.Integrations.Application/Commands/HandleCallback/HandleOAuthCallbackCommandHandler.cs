using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Oauth;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.Errors;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.DataProtection.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.HandleCallback;

/// <summary>
/// Validates and consumes the state, exchanges the code for tokens, and upserts the connected
/// account with the credentials encrypted. Re-running for an already-connected provider reconnects it
/// (a widened-scope re-consent).
/// </summary>
public sealed class HandleOAuthCallbackCommandHandler(
    IUnitOfWorkFactory factory,
    OAuthProviderRegistry registry,
    ISecretProtector protector,
    TimeProvider timeProvider)
    : CommandHandlerBase<HandleOAuthCallbackCommand, string>
{
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

        var scopes = ScopeString.Join(tokens.Scopes);
        var accessEnc = protector.Protect(tokens.AccessToken);
        var refreshEnc = tokens.RefreshToken is null ? null : protector.Protect(tokens.RefreshToken);

        await factory.ExecuteAsync(IntegrationsModule.Name, async (context, token) =>
        {
            var accounts = context.AcquireRepository<IExternalAccountRepository>();
            var existing = await accounts.FindAsync(consumed.UserId, provider.Name, token);

            if (existing is null)
            {
                var account = ExternalAccount.ConnectOAuth(
                    consumed.UserId, provider.Name,
                    tokens.ProviderAccountId ?? consumed.UserId.ToString(),
                    tokens.DisplayName, scopes, accessEnc, tokens.ExpiresAt, refreshEnc, timeProvider);
                await accounts.AddAsync(account, token);
            }
            else
            {
                existing.ReconnectOAuth(tokens.DisplayName, scopes, accessEnc, tokens.ExpiresAt, refreshEnc, timeProvider);
                await accounts.UpdateAsync(existing, token);
            }

            return true;
        }, cancellationToken: ct);

        return Ok(consumed.RedirectAfter);
    }

    private Task<OAuthState?> ConsumeStateAsync(string state, string provider, CancellationToken ct) =>
        factory.ExecuteAsync(IntegrationsModule.Name, async (context, token) =>
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
