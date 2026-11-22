using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetLinkPreview;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

/// <summary>Links pasted into a page: what the linked page says about itself, for a bookmark or an embed.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/links")]
public sealed class LinksController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet("preview")]
    public async Task<IActionResult> PreviewAsync([FromQuery] string url, CancellationToken ct)
    {
        var result = await sender.Send(new GetLinkPreviewQuery(new GetLinkPreviewInput(UserId, url)), ct);
        return result.ToActionResult(errorMapper);
    }
}
