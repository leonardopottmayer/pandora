using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Commands.DisconnectAccount;
using Pottmayer.Pandora.Modules.Integrations.Application.Commands.HandleCallback;
using Pottmayer.Pandora.Modules.Integrations.Application.Commands.SaveApiKey;
using Pottmayer.Pandora.Modules.Integrations.Application.Commands.StartConnection;
using Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetAccounts;
using Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetEvents;
using Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetProviders;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Integrations.Presentation.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/integrations")]
public sealed class IntegrationsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor,
    IOptions<IntegrationsOptions> options) : ControllerBase
{
    /// <summary>Available providers and whether the user has connected each, for the settings catalog.</summary>
    [Authorize]
    [HttpGet("providers")]
    public async Task<IActionResult> GetProvidersAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetProvidersQuery(new GetProvidersInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The user's connected accounts, with status and last error.</summary>
    [Authorize]
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccountsAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetAccountsQuery(new GetAccountsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The recent connection event log (connect/refresh-failure/revoke/disconnect), newest first.</summary>
    [Authorize]
    [HttpGet("events")]
    public async Task<IActionResult> GetEventsAsync([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetIntegrationEventsQuery(new GetIntegrationEventsInput(UserId, limit)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>
    /// Starts a connection: returns the provider consent URL for the SPA to send the browser to.
    /// Re-running for an already-connected provider re-consents (e.g. to widen scopes).
    /// </summary>
    [Authorize]
    [HttpPost("{provider}/connect")]
    public async Task<IActionResult> ConnectAsync(string provider, [FromBody] ConnectRequest body, CancellationToken ct)
    {
        var command = new StartConnectionCommand(
            new StartConnectionInput(UserId, provider, body.RedirectAfter, body.Scopes));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>
    /// The provider's redirect target. Anonymous: it authenticates by the single-use <c>state</c> it
    /// issued. On success it 302s the browser back into the SPA.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> CallbackAsync(
        string provider,
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Redirect(Home("error"));

        var command = new HandleOAuthCallbackCommand(new HandleOAuthCallbackInput(provider, code, state));
        var result = await sender.Send(command, ct);

        return result.IsSuccess
            ? Redirect(Absolute(result.Value ?? "/"))
            : Redirect(Home("error"));
    }

    /// <summary>
    /// Stores (or replaces) the user's API key for an <c>api-key</c> provider such as Gemini. The key is
    /// protected before it is persisted; the response never echoes it back.
    /// </summary>
    [Authorize]
    [HttpPut("{provider}/api-key")]
    public async Task<IActionResult> SaveApiKeyAsync(string provider, [FromBody] SaveApiKeyRequest body, CancellationToken ct)
    {
        var command = new SaveApiKeyCommand(new SaveApiKeyInput(UserId, provider, body.ApiKey));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Revokes the connection at the provider and deletes it locally.</summary>
    [Authorize]
    [HttpDelete("accounts/{id:guid}")]
    public async Task<IActionResult> DisconnectAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DisconnectAccountCommand(new DisconnectAccountInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    private string Absolute(string relativePath)
    {
        var baseUrl = options.Value.SpaBaseUrl;
        return string.IsNullOrWhiteSpace(baseUrl) ? relativePath : $"{baseUrl.TrimEnd('/')}{relativePath}";
    }

    private string Home(string outcome)
    {
        var baseUrl = options.Value.SpaBaseUrl;
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "/" : baseUrl.TrimEnd('/') + "/";
        return $"{root}?integration={outcome}";
    }

    /// <summary>Body for starting a connection.</summary>
    public sealed record ConnectRequest(string RedirectAfter, IReadOnlyList<string>? Scopes);

    /// <summary>Body for saving an API key.</summary>
    public sealed record SaveApiKeyRequest(string ApiKey);
}
