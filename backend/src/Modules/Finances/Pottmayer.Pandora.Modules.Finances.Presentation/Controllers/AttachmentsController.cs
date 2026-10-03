using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAttachment;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UploadAttachment;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAttachmentContent;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAttachments;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Finances.Presentation.Controllers;

/// <summary>Files attached to a transaction or a pending transaction: boletos, receipts, invoices.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/finances/attachments")]
public sealed class AttachmentsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    /// <summary>The attachments of one transaction or one pending transaction (pass exactly one), oldest first.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? transactionId, [FromQuery] Guid? pendingTransactionId, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetAttachmentsQuery(new GetAttachmentsInput(UserId, transactionId, pendingTransactionId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Attaches an image or PDF to a transaction or a pending transaction (exactly one).</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        [FromForm] string kind,
        [FromForm] Guid? transactionId,
        [FromForm] Guid? pendingTransactionId,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        var result = await sender.Send(new UploadAttachmentCommand(new UploadAttachmentInput(
            UserId, transactionId, pendingTransactionId, kind, file.FileName, contentType, ms.ToArray())), ct);

        return result.ToActionResult(errorMapper);
    }

    /// <summary>Serves the attachment's bytes inline, with its stored content type and file name.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> DownloadAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetAttachmentContentQuery(new GetAttachmentContentInput(UserId, id)), ct);

        if (result.IsFailure)
        {
            var error = result.Errors[0];
            return StatusCode(errorMapper.MapToStatusCode(error.Type), errorMapper.Map(error));
        }

        var content = result.Value!;
        Response.Headers.ContentDisposition =
            new ContentDisposition { Inline = true, FileName = content.FileName }.ToString();

        return File(content.Content, content.ContentType);
    }

    /// <summary>Removes an attachment and its bytes.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteAttachmentCommand(new DeleteAttachmentInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }
}
