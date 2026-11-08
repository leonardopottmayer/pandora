using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.MoveTreeItems;
using Pottmayer.Pandora.Modules.Notes.Presentation.Filters;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

[ApiController]
[Authorize]
[TypeFilter(typeof(PageHistoryConflictFilter))]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/tree")]
public sealed class TreeController(ISender sender, IHttpErrorMapper errorMapper, IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    [HttpPost("move")]
    public async Task<IActionResult> MoveAsync(MoveTreeItemsRequest request, CancellationToken ct) =>
        (await sender.Send(new MoveTreeItemsCommand(new MoveTreeItemsInput(userContextAccessor.Context.User!.Id,
            request.SpaceId, request.Kind, request.ParentId, request.FolderId, request.ItemIds)), ct)).ToActionResult(errorMapper);
}
