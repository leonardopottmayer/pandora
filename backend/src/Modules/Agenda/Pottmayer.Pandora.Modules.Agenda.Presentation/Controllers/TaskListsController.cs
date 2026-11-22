using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskLists;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda/task-lists")]
public sealed class TaskListsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The user's task lists, by manual position then name.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTaskListsQuery(new GetTaskListsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Creates a task list.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateTaskListRequest body, CancellationToken ct)
    {
        var command = new CreateTaskListCommand(
            new CreateTaskListInput(UserId, body.Name, body.IsDefault, body.Position));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Patches a task list (rename, reorder, set default, archive).</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateTaskListRequest body, CancellationToken ct)
    {
        var command = new UpdateTaskListCommand(
            new UpdateTaskListInput(UserId, id, body.Name, body.Position, body.IsDefault, body.Archive));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Deletes an empty task list.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteTaskListCommand(new DeleteTaskListInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    public sealed record CreateTaskListRequest(string Name, bool IsDefault = false, int Position = 0);
    public sealed record UpdateTaskListRequest(string? Name, int? Position, bool? IsDefault, bool Archive = false);
}
