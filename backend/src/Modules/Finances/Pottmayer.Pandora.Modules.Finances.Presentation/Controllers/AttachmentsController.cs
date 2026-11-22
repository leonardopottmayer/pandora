using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.AssignAttachment;
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

/// <summary>
/// Files attached to a transaction, a pending transaction or a card statement — boletos, receipts, invoices —
/// and the queue of files shared with the assistant bot, waiting to be filed under one of them.
/// </summary>
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

    /// <summary>
    /// The attachments of one transaction, pending transaction or card statement — or the queue with
    /// <c>queued=true</c> (pass exactly one) — oldest first.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? transactionId, [FromQuery] Guid? pendingTransactionId, [FromQuery] Guid? cardStatementId,
        [FromQuery] bool queued, CancellationToken ct)
    {
        var result = await sender.Send(new GetAttachmentsQuery(
            new GetAttachmentsInput(UserId, transactionId, pendingTransactionId, cardStatementId, queued)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>
    /// Attaches an image or PDF to a transaction, a pending transaction or a card statement (at most one; none
    /// queues it).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        [FromForm] string kind,
        [FromForm] Guid? transactionId,
        [FromForm] Guid? pendingTransactionId,
        [FromForm] Guid? cardStatementId,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        var result = await sender.Send(new UploadAttachmentCommand(new UploadAttachmentInput(
            UserId, transactionId, pendingTransactionId, cardStatementId, kind, file.FileName, contentType, ms.ToArray())), ct);

        return result.ToActionResult(errorMapper);
    }

    /// <summary>Files a queued attachment under one transaction, pending transaction or card statement.</summary>
    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> AssignAsync(Guid id, [FromBody] AssignAttachmentRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new AssignAttachmentCommand(new AssignAttachmentInput(
            UserId, id, request.TransactionId, request.PendingTransactionId, request.CardStatementId)), ct);
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

    public sealed record AssignAttachmentRequest(Guid? TransactionId, Guid? PendingTransactionId, Guid? CardStatementId);
}
