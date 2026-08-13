using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.UploadAttachment;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetAttachment;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/attachments")]
public sealed class AttachmentsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        [FromForm] Guid? pageId,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        var result = await sender.Send(new UploadAttachmentCommand(new UploadAttachmentInput(
            UserId, pageId, file.FileName, contentType, ms.ToArray())), ct);

        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> DownloadAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetAttachmentQuery(new GetAttachmentInput(id)), ct);

        if (result.IsFailure)
        {
            var error = result.Errors[0];
            return StatusCode(errorMapper.MapToStatusCode(error.Type), errorMapper.Map(error));
        }

        var content = result.Value!;

        // Inline (not "attachment") so an embedded image renders in place; the filename still rides
        // along for files the user chooses to save (zip, pdf).
        Response.Headers.ContentDisposition =
            new ContentDisposition { Inline = true, FileName = content.FileName }.ToString();

        return File(content.Content, content.ContentType);
    }
}
