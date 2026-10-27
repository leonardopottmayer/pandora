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

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.StartConnection;

/// <summary>
/// Builds the provider consent URL and persists the in-flight state (CSRF token + encrypted PKCE
/// verifier + the targeted account, if any). The SPA never sees a token; the browser is sent to the
/// provider. A targeted account is hinted to the provider so the user lands on the right login.
/// </summary>
public sealed class StartConnectionCommandHandler(
    IUnitOfWorkFactory factory,
    OAuthProviderRegistry registry,
    ISecretProtector protector,
    TimeProvider timeProvider)
    : CommandHandlerBase<StartConnectionCommand, string>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    protected override async Task<Result<string>> HandleAsync(StartConnectionCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!registry.TryGet(input.Provider, out var provider))
            return Fail(IntegrationErrors.UnknownProvider(input.Provider));

        // Guard against open redirects: only a relative path is allowed back into the SPA.
        if (!IsRelativePath(input.RedirectAfter))
            return Fail(IntegrationErrors.InvalidRedirect);

        string? loginHint = null;
        if (input.AccountId is { } accountId)
        {
            var target = await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
                await context.AcquireRepository<IExternalAccountRepository>().GetOwnedAsync(input.UserId, accountId, token),
                cancellationToken: ct);

            if (target is null)
                return Fail(IntegrationErrors.AccountNotFound);
            if (target.AuthKind != AuthKind.OAuth || !string.Equals(target.Provider, provider.Name, StringComparison.OrdinalIgnoreCase))
                return Fail(IntegrationErrors.AccountProviderMismatch);

            loginHint = target.DisplayName;
        }

        var scopes = input.Scopes is { Count: > 0 } ? input.Scopes : provider.DefaultScopes;

        var state = PkceCodes.NewState();
        var verifier = PkceCodes.NewVerifier();
        var challenge = PkceCodes.Challenge(verifier);

        var url = provider.BuildAuthorizationUrl(new OAuthAuthorizationRequest(state, challenge, scopes, loginHint));

        await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, token) =>
        {
            var states = context.AcquireRepository<IOAuthStateRepository>();
            var pending = OAuthState.Issue(
                input.UserId,
                provider.Name,
                state,
                protector.Protect(verifier),
                input.RedirectAfter,
                input.AccountId,
                timeProvider.GetUtcNow().Add(Ttl));
            await states.AddAsync(pending, token);
            return true;
        }, cancellationToken: ct);

        return Ok(url.ToString());
    }

    private static bool IsRelativePath(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith('/')
        && !value.StartsWith("//", StringComparison.Ordinal)
        && !Uri.TryCreate(value, UriKind.Absolute, out _);
}
