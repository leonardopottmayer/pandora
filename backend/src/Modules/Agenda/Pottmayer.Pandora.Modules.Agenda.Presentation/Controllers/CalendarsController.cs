using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendars;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda/calendars")]
public sealed class CalendarsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The user's calendars.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetCalendarsQuery(new GetCalendarsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Creates a calendar.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateCalendarRequest body, CancellationToken ct)
    {
        var command = new CreateCalendarCommand(new CreateCalendarInput(
            UserId, body.Name, body.Color, body.IsDefault, body.TimeZone));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Edits a calendar; pass <c>archive=true</c> to hide it.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateCalendarRequest body, CancellationToken ct)
    {
        var command = new UpdateCalendarCommand(new UpdateCalendarInput(
            UserId, id, body.Name, body.Color, body.IsVisible, body.TimeZone, body.IsDefault, body.Archive));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Deletes an empty calendar. Refused while it still has events — archive instead.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteCalendarCommand(new DeleteCalendarInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    public sealed record CreateCalendarRequest(
        string Name, string? Color = null, bool IsDefault = false, string? TimeZone = null);

    public sealed record UpdateCalendarRequest(
        string? Name = null,
        string? Color = null,
        bool? IsVisible = null,
        string? TimeZone = null,
        bool? IsDefault = null,
        bool Archive = false);
}
