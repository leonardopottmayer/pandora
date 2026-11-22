using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreateSpace;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.DeleteSpace;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SetDefaultSpace;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SetSpaceArchived;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdateSpace;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetSpaces;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

/// <summary>The spaces pages live in. A page's space is set on the page endpoints (create, move).</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/spaces")]
public sealed class SpacesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetSpacesQuery(new GetSpacesInput(UserId, includeArchived)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(SaveSpaceRequest request, CancellationToken ct)
    {
        var command = new CreateSpaceCommand(new CreateSpaceInput(
            UserId, request.Name, request.Icon, request.Color, request.Description));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, SaveSpaceRequest request, CancellationToken ct)
    {
        var command = new UpdateSpaceCommand(new UpdateSpaceInput(
            UserId, id, request.Name, request.Icon, request.Color, request.Description));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/default")]
    public async Task<IActionResult> SetDefaultAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SetDefaultSpaceCommand(new SetDefaultSpaceInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SetSpaceArchivedCommand(new SetSpaceArchivedInput(UserId, id, Archived: true)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SetSpaceArchivedCommand(new SetSpaceArchivedInput(UserId, id, Archived: false)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteSpaceCommand(new DeleteSpaceInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }
}
