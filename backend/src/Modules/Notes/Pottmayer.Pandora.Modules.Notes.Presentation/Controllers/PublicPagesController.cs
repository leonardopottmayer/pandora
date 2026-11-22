using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.PageViews;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

/// <summary>
/// Pages opened by their public link, without signing in: the page, read-only, and the library items
/// it shows. A closed link, a deleted page or an item the page does not use answer 404.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/notes/pages/{token}")]
public sealed class PublicPagesController(ISender sender, IHttpErrorMapper errorMapper) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(string token, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new GetPublicPageQuery(new GetPublicPageInput(token)), ct)).ToActionResult(errorMapper);
    }

    [HttpGet("media/{mediaId:guid}")]
    public Task<IActionResult> MediaAsync(string token, Guid mediaId, CancellationToken ct) => ServeAsync(token, mediaId, null, ct);

    [HttpGet("media/{mediaId:guid}/thumbnail")]
    public Task<IActionResult> ThumbnailAsync(string token, Guid mediaId, CancellationToken ct) => ServeAsync(token, mediaId, RenditionPurpose.Thumbnail, ct);

    private async Task<IActionResult> ServeAsync(string token, Guid mediaId, string? purpose, CancellationToken ct)
    {
        var result = await sender.Send(new GetPublicMediaQuery(new GetPublicMediaInput(token, mediaId, purpose)), ct);
        if (result.IsFailure)
        {
            var error = result.Errors[0];
            return StatusCode(errorMapper.MapToStatusCode(error.Type), errorMapper.Map(error));
        }
        var content = result.Value!;
        Response.Headers.ContentDisposition = new ContentDisposition { Inline = true, FileName = content.FileName }.ToString();
        // Not cached for good: closing the link must stop the bytes too. nosniff keeps a file a file.
        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(content.Content, content.ContentType);
    }
}
