using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.BindGoogleCalendars;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncCalendarBinding;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkCalendar;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendarBindings;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetGoogleCalendars;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetSyncConflicts;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

/// <summary>Google Calendar → Pandora: which Google calendars feed which Pandora calendars.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda/google")]
public sealed class GoogleCalendarController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The calendars a connected Google account can see, marking those already linked.</summary>
    [HttpGet("calendars")]
    public async Task<IActionResult> GetCalendarsAsync([FromQuery] Guid accountId, CancellationToken ct)
    {
        var result = await sender.Send(new GetGoogleCalendarsQuery(new GetGoogleCalendarsInput(UserId, accountId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The user's Google calendar links, with their sync status.</summary>
    [HttpGet("bindings")]
    public async Task<IActionResult> GetBindingsAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetCalendarBindingsQuery(new GetCalendarBindingsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Events both sides changed between two syncs, newest first, with the version that lost.</summary>
    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflictsAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetSyncConflictsQuery(new GetSyncConflictsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Brings Google calendars into Pandora, each as its own calendar. Their events arrive on the first sync.</summary>
    [HttpPost("bindings")]
    public async Task<IActionResult> BindAsync([FromBody] BindRequest body, CancellationToken ct)
    {
        var command = new BindGoogleCalendarsCommand(new BindGoogleCalendarsInput(UserId, body.AccountId, body.CalendarIds ?? []));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Syncs a link now, resuming it if it had stopped.</summary>
    [HttpPost("bindings/{id:guid}/sync")]
    public async Task<IActionResult> SyncAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SyncCalendarBindingNowCommand(new SyncCalendarBindingNowInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Stops syncing; the calendar and its events stay as a local calendar.</summary>
    [HttpDelete("bindings/{id:guid}")]
    public async Task<IActionResult> UnlinkAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new UnlinkCalendarCommand(new UnlinkCalendarInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    public sealed record BindRequest(Guid AccountId, IReadOnlyList<string>? CalendarIds);
}
