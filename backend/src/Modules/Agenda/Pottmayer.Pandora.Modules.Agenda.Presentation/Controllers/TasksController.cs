using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.ReopenTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda/tasks")]
public sealed class TasksController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The user's tasks, optionally filtered by list, status and a coarse due bucket.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] Guid? listId,
        [FromQuery] TaskItemStatus? status,
        [FromQuery] TaskDueBucket? due,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetTasksQuery(new GetTasksInput(UserId, listId, status, due)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Creates a task (or a subtask when <c>ParentTaskId</c> is given).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateTaskRequest body, CancellationToken ct)
    {
        var command = new CreateTaskCommand(new CreateTaskInput(
            UserId, body.ListId, body.ParentTaskId, body.Title, body.Notes, body.DueAt, body.DueHasTime,
            ParsePriority(body.Priority), body.TimeZone, body.Rrule, body.Position));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Edits a task's core fields.</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateTaskRequest body, CancellationToken ct)
    {
        var command = new UpdateTaskCommand(new UpdateTaskInput(
            UserId, id, body.Title, body.Notes, body.DueAt, body.DueHasTime, ParsePriority(body.Priority)));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Completes a task; a recurring task also spawns its next instance.</summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new CompleteTaskCommand(new CompleteTaskInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Reopens a completed task.</summary>
    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> ReopenAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ReopenTaskCommand(new ReopenTaskInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Soft-deletes a task.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteTaskCommand(new DeleteTaskInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    private static TaskPriority ParsePriority(string? priority) =>
        Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out var p) ? p : TaskPriority.None;

    public sealed record CreateTaskRequest(
        Guid ListId,
        string Title,
        string? Notes = null,
        Guid? ParentTaskId = null,
        DateTimeOffset? DueAt = null,
        bool DueHasTime = false,
        string? Priority = null,
        string? TimeZone = null,
        string? Rrule = null,
        int Position = 0);

    public sealed record UpdateTaskRequest(
        string Title,
        string? Notes = null,
        DateTimeOffset? DueAt = null,
        bool DueHasTime = false,
        string? Priority = null);
}
