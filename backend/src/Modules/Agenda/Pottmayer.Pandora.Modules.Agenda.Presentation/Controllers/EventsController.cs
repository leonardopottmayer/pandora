using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvents;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda/events")]
public sealed class EventsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>Expanded occurrences overlapping <c>[from, to]</c>, optionally limited to some calendars.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] List<Guid>? calendarIds,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetEventsQuery(new GetEventsInput(UserId, from, to, calendarIds)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The event <em>row</em> (series), including its rrule/recurrence — the occurrence reads omit these.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetEventQuery(new GetEventInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Creates an event (a single occurrence, or a series when <c>Rrule</c> is set).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateEventRequest body, CancellationToken ct)
    {
        var command = new CreateEventCommand(new CreateEventInput(
            UserId, body.CalendarId, body.Title, body.Description, body.Location, body.Url,
            body.StartsAt, body.EndsAt, body.IsAllDay, body.TimeZone, body.Rrule, body.Status));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Edits an event within a scope (<c>this</c> | <c>this-and-future</c> | <c>all</c>, default <c>all</c>).</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromQuery] string? scope,
        [FromQuery] DateTimeOffset? occurrenceStart,
        [FromBody] UpdateEventRequest body,
        CancellationToken ct)
    {
        if (!EventEditScopeParser.TryParse(scope, out var parsed))
            return Result<bool>.Failure([EventErrors.InvalidScope]).ToActionResult(errorMapper);

        var command = new UpdateEventCommand(new UpdateEventInput(
            UserId, id, parsed, occurrenceStart, body.Title, body.Description, body.Location, body.Url,
            body.StartsAt, body.EndsAt, body.IsAllDay, body.CalendarId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Deletes an event within a scope (<c>this</c> | <c>this-and-future</c> | <c>all</c>, default <c>all</c>).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        [FromQuery] string? scope,
        [FromQuery] DateTimeOffset? occurrenceStart,
        CancellationToken ct)
    {
        if (!EventEditScopeParser.TryParse(scope, out var parsed))
            return Result<bool>.Failure([EventErrors.InvalidScope]).ToActionResult(errorMapper);

        var command = new DeleteEventCommand(new DeleteEventInput(UserId, id, parsed, occurrenceStart));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    public sealed record CreateEventRequest(
        Guid CalendarId,
        string Title,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        string? Description = null,
        string? Location = null,
        string? Url = null,
        bool IsAllDay = false,
        string? TimeZone = null,
        string? Rrule = null,
        string? Status = null);

    public sealed record UpdateEventRequest(
        string? Title = null,
        string? Description = null,
        string? Location = null,
        string? Url = null,
        DateTimeOffset? StartsAt = null,
        DateTimeOffset? EndsAt = null,
        bool? IsAllDay = null,
        Guid? CalendarId = null);
}
