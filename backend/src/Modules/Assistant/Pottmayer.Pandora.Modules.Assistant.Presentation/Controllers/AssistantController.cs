using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.SaveProfile;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.TestProvider;
using Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetProfile;
using Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetProviders;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Assistant.Presentation.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assistant")]
public sealed class AssistantController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The user's assistant configuration. Returns defaults when none has been saved yet.</summary>
    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfileAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetProfileQuery(new GetProfileInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Creates or replaces the user's assistant configuration (provider, model, options).</summary>
    [Authorize]
    [HttpPost("profile")]
    public async Task<IActionResult> SaveProfileAsync([FromBody] SaveProfileRequest body, CancellationToken ct)
    {
        var command = new SaveProfileCommand(new SaveProfileInput(
            UserId, body.Provider, body.Model, body.IsEnabled, body.LocaleOverride, body.ConfirmationLevel));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The chat providers the assistant supports and whether the user has a key stored for each.</summary>
    [Authorize]
    [HttpGet("providers")]
    public async Task<IActionResult> GetProvidersAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetProvidersQuery(new GetProvidersInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>
    /// Runs a reachability probe against a provider: one minimal round-trip with the user's stored key,
    /// reporting ok/error and latency. An explicit action (not the GET) because it makes a real,
    /// possibly billed call to the provider.
    /// </summary>
    [Authorize]
    [HttpPost("providers/{provider}/test")]
    public async Task<IActionResult> TestProviderAsync(string provider, [FromBody] TestProviderRequest? body, CancellationToken ct)
    {
        var command = new TestProviderCommand(new TestProviderInput(UserId, provider, body?.Model));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    /// <summary>Body for saving the assistant profile.</summary>
    public sealed record SaveProfileRequest(
        string Provider,
        string Model,
        bool IsEnabled,
        string? LocaleOverride,
        string ConfirmationLevel);

    /// <summary>Body for the reachability test. Model is optional; the default is used when omitted.</summary>
    public sealed record TestProviderRequest(string? Model);
}
