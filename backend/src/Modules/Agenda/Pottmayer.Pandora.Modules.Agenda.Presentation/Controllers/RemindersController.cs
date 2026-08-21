using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CancelReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetReminders;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda/reminders")]
public sealed class RemindersController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The user's reminders, newest remind time first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetRemindersQuery(new GetRemindersInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Creates a reminder that fires at the given instant.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateReminderRequest body, CancellationToken ct)
    {
        var timeZone = string.IsNullOrWhiteSpace(body.TimeZone) ? CultureInfo.CurrentUICulture.Name : body.TimeZone;
        var command = new CreateReminderCommand(
            new CreateReminderInput(UserId, body.Title, body.Notes, body.RemindAt, timeZone));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Acknowledges a reminder.</summary>
    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new AcknowledgeReminderCommand(new AcknowledgeReminderInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Snoozes a reminder until the given instant.</summary>
    [HttpPost("{id:guid}/snooze")]
    public async Task<IActionResult> SnoozeAsync(Guid id, [FromBody] SnoozeReminderRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new SnoozeReminderCommand(new SnoozeReminderInput(UserId, id, body.Until)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Cancels a reminder before it is acted on.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new CancelReminderCommand(new CancelReminderInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    public sealed record CreateReminderRequest(string Title, string? Notes, DateTimeOffset RemindAt, string? TimeZone);
    public sealed record SnoozeReminderRequest(DateTimeOffset Until);
}
