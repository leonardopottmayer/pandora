using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.BindGoogleTaskLists;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncTaskListBinding;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetGoogleTaskLists;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskListBindings;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

/// <summary>Google Tasks ⇄ Pandora: which Google task lists sync with which Pandora lists.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda/google")]
public sealed class GoogleTasksController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The task lists of a connected Google account, marking those already linked.</summary>
    [HttpGet("task-lists")]
    public async Task<IActionResult> GetTaskListsAsync([FromQuery] Guid accountId, CancellationToken ct)
    {
        var result = await sender.Send(new GetGoogleTaskListsQuery(new GetGoogleTaskListsInput(UserId, accountId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The user's Google task list links, with their sync status.</summary>
    [HttpGet("task-list-bindings")]
    public async Task<IActionResult> GetBindingsAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTaskListBindingsQuery(new GetTaskListBindingsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Syncs Google task lists with Pandora, each as its own list. Their tasks arrive on the first sync.</summary>
    [HttpPost("task-list-bindings")]
    public async Task<IActionResult> BindAsync([FromBody] BindTaskListsRequest body, CancellationToken ct)
    {
        var command = new BindGoogleTaskListsCommand(new BindGoogleTaskListsInput(UserId, body.AccountId, body.ListIds ?? []));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Syncs a link now, resuming it if it had stopped.</summary>
    [HttpPost("task-list-bindings/{id:guid}/sync")]
    public async Task<IActionResult> SyncAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SyncTaskListBindingNowCommand(new SyncTaskListBindingNowInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Stops syncing; the list and its tasks stay.</summary>
    [HttpDelete("task-list-bindings/{id:guid}")]
    public async Task<IActionResult> UnlinkAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new UnlinkTaskListCommand(new UnlinkTaskListInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    public sealed record BindTaskListsRequest(Guid AccountId, IReadOnlyList<string>? ListIds);
}
