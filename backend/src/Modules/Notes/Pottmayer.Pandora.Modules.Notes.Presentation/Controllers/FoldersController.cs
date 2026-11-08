using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreateFolder;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.DeleteFolder;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.MoveFolder;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdateFolder;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetFolders;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetFolderProperties;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

/// <summary>The folders of a space. A page's folder is set on the page endpoints (create, move).</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/folders")]
public sealed class FoldersController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet("{id:guid}/properties")]
    public async Task<IActionResult> PropertiesAsync(Guid id, [FromQuery] bool includeArchived = false,
        [FromQuery] Guid[]? tagIds = null, CancellationToken ct = default) =>
        (await sender.Send(new GetFolderPropertiesQuery(new GetFolderPropertiesInput(UserId, id, includeArchived, tagIds)), ct)).ToActionResult(errorMapper);

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] Guid? spaceId = null, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetFoldersQuery(new GetFoldersInput(UserId, spaceId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(SaveFolderRequest request, CancellationToken ct)
    {
        var command = new CreateFolderCommand(new CreateFolderInput(
            UserId, request.Name, request.ParentId, request.SpaceId, request.Icon, request.Description));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, SaveFolderRequest request, CancellationToken ct)
    {
        var command = new UpdateFolderCommand(new UpdateFolderInput(UserId, id, request.Name, request.Icon, request.Description));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> MoveAsync(Guid id, MoveFolderRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new MoveFolderCommand(new MoveFolderInput(UserId, id, request.ParentId, request.OrderIndex)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteFolderCommand(new DeleteFolderInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }
}
