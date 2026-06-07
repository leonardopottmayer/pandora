using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Challenge;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Disable;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Enable;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.RecoveryCodes;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Setup;
using Pottmayer.Pandora.Modules.Identity.Application.Queries.Mfa.GetMfaStatus;
using Pottmayer.Pandora.Modules.Identity.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Identity.Presentation.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/identity/mfa")]
public sealed class MfaController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> StatusAsync(CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var query = new GetMfaStatusQuery(new GetMfaStatusInput(userId));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    [Authorize]
    [HttpPost("setup")]
    public async Task<IActionResult> SetupAsync(CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var command = new SetupMfaCommand(new SetupMfaInput(userId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [Authorize]
    [HttpPost("enable")]
    public async Task<IActionResult> EnableAsync(EnableMfaRequest request, CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var command = new EnableMfaCommand(new EnableMfaInput(userId, request.Code));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [Authorize]
    [HttpPost("disable")]
    public async Task<IActionResult> DisableAsync(DisableMfaRequest request, CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var command = new DisableMfaCommand(new DisableMfaInput(userId, request.Password, request.Code));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [Authorize]
    [HttpPost("recovery-codes")]
    public async Task<IActionResult> RegenerateRecoveryCodesAsync(RegenerateRecoveryCodesRequest request, CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var command = new RegenerateRecoveryCodesCommand(
            new RegenerateRecoveryCodesInput(userId, request.Password, request.Code));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [AllowAnonymous]
    [HttpPost("challenge")]
    public async Task<IActionResult> ChallengeAsync(MfaChallengeRequest request, CancellationToken ct)
    {
        var command = new CompleteMfaChallengeCommand(new CompleteMfaChallengeInput(request.Ticket, request.Code));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
