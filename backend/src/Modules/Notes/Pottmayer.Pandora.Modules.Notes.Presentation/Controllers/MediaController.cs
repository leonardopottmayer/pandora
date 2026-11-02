using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.ManageMedia;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetMedia;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

/// <summary>The media library: the images and files notes embed, organized in virtual folders.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/media")]
public sealed class MediaController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    /// <summary>A folder's own items with <c>folderId</c>, the unsorted ones with <c>unsorted</c>, else all.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? folderId,
        [FromQuery] bool unsorted = false,
        [FromQuery] string? kind = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListMediaQuery(new ListMediaInput(UserId, folderId, unsorted, kind, q)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAsync(IFormFile file, [FromForm] Guid? folderId, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        var result = await sender.Send(new UploadMediaCommand(new UploadMediaInput(
            UserId, folderId, file.FileName, contentType, ms.ToArray())), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public Task<IActionResult> RenameAsync(Guid id, RenameMediaRequest request, CancellationToken ct)
        => ChangeAsync(id, new MediaChange.Rename(request.Name), ct);

    [HttpPost("{id:guid}/move")]
    public Task<IActionResult> MoveAsync(Guid id, MoveMediaRequest request, CancellationToken ct)
        => ChangeAsync(id, new MediaChange.Move(request.FolderId), ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
        => ChangeAsync(id, new MediaChange.Delete(), ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> ContentAsync(Guid id, CancellationToken ct) => ServeAsync(id, purpose: null, ct);

    [HttpGet("{id:guid}/thumbnail")]
    public Task<IActionResult> ThumbnailAsync(Guid id, CancellationToken ct) => ServeAsync(id, RenditionPurpose.Thumbnail, ct);

    private async Task<IActionResult> ChangeAsync(Guid id, MediaChange change, CancellationToken ct)
    {
        var result = await sender.Send(new ChangeMediaCommand(new ChangeMediaInput(UserId, id, change)), ct);
        return result.ToActionResult(errorMapper);
    }

    private async Task<IActionResult> ServeAsync(Guid id, string? purpose, CancellationToken ct)
    {
        var result = await sender.Send(new GetMediaContentQuery(new GetMediaContentInput(UserId, id, purpose)), ct);
        if (result.IsFailure)
        {
            var error = result.Errors[0];
            return StatusCode(errorMapper.MapToStatusCode(error.Type), errorMapper.Map(error));
        }

        var content = result.Value!;

        // Inline (not "attachment") so an embedded image renders in place; the file name still rides
        // along for files the user saves. An id addresses bytes that never change, so the browser may
        // keep them for good — which is what keeps a library of thumbnails cheap to open again.
        Response.Headers.ContentDisposition = new ContentDisposition { Inline = true, FileName = content.FileName }.ToString();
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return File(content.Content, content.ContentType);
    }
}

/// <summary>The folders of the media library.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/media-folders")]
public sealed class MediaFoldersController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetMediaFoldersQuery(new GetMediaFoldersInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public Task<IActionResult> CreateAsync(CreateMediaFolderRequest request, CancellationToken ct)
        => ChangeAsync(null, new FolderChange.Create(request.Name, request.ParentId), ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> RenameAsync(Guid id, RenameMediaFolderRequest request, CancellationToken ct)
        => ChangeAsync(id, new FolderChange.Rename(request.Name), ct);

    [HttpPost("{id:guid}/move")]
    public Task<IActionResult> MoveAsync(Guid id, MoveMediaFolderRequest request, CancellationToken ct)
        => ChangeAsync(id, new FolderChange.Move(request.ParentId), ct);

    /// <summary>What the folder holds moves up a level; nothing is deleted with it.</summary>
    [HttpDelete("{id:guid}")]
    public Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
        => ChangeAsync(id, new FolderChange.Delete(), ct);

    private async Task<IActionResult> ChangeAsync(Guid? id, FolderChange change, CancellationToken ct)
    {
        var result = await sender.Send(new ChangeMediaFolderCommand(new ChangeMediaFolderInput(UserId, id, change)), ct);
        return result.ToActionResult(errorMapper);
    }
}
