using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.Trash;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

/// <summary>Deleted pages: brought back, or deleted for good.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/trash")]
public sealed class TrashController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
        => (await sender.Send(new GetTrashQuery(new GetTrashInput(UserId)), ct)).ToActionResult(errorMapper);

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new RestoreFromTrashCommand(new RestoreFromTrashInput(UserId, id)), ct)).ToActionResult(errorMapper);

    /// <summary>Deletes one page in the trash for good, with what is below it; answers how many pages went.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new DeleteFromTrashCommand(new DeleteFromTrashInput(UserId, id)), ct)).ToActionResult(errorMapper);

    /// <summary>Empties the trash; answers how many pages went.</summary>
    [HttpDelete]
    public async Task<IActionResult> EmptyAsync(CancellationToken ct)
        => (await sender.Send(new DeleteFromTrashCommand(new DeleteFromTrashInput(UserId, null)), ct)).ToActionResult(errorMapper);
}
