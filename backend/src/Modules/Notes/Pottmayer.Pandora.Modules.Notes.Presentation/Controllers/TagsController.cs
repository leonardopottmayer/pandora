using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SetTagColor;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetTags;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

/// <summary>
/// Tags are written in the pages' markdown, so there is no create and no delete here: the text
/// creates them and the sweep removes them. What is left is listing them and painting them.
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/tags")]
public sealed class TagsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTagsQuery(new GetTagsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}/color")]
    public async Task<IActionResult> SetColorAsync(Guid id, SetTagColorRequest request, CancellationToken ct)
    {
        var command = new SetTagColorCommand(new SetTagColorInput(UserId, id, request.Color));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
