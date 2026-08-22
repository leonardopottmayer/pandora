using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateAlert;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteAlert;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetAlerts;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda")]
public sealed class AlertsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>Lists the alerts on a subject. <c>task</c> and <c>event</c> are supported in this version.</summary>
    [HttpGet("{subjectType}/{id:guid}/alerts")]
    public async Task<IActionResult> GetAsync(string subjectType, Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetAlertsQuery(new GetAlertsInput(UserId, subjectType, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Adds an alert to a subject. <c>task</c> and <c>event</c> are supported in this version.</summary>
    [HttpPost("{subjectType}/{id:guid}/alerts")]
    public async Task<IActionResult> CreateAsync(
        string subjectType, Guid id, [FromBody] CreateAlertRequest body, CancellationToken ct)
    {
        var command = new CreateAlertCommand(
            new CreateAlertInput(UserId, subjectType, id, body.OffsetMinutes, body.Channels));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Removes an alert.</summary>
    [HttpDelete("alerts/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteAlertCommand(new DeleteAlertInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    public sealed record CreateAlertRequest(int OffsetMinutes, string[]? Channels = null);
}
