using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.ApplyMessageAction;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.ConnectMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.DeleteMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageLabels;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.OpenMessage;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SendMessage;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailboxNow;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.UpdateMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetAttachment;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetComposeDraft;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetLabels;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetMailboxes;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.ListMessages;
using Pottmayer.Pandora.Modules.Communications.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Communications.Presentation.Controllers;

/// <summary>The user's mailboxes and their messages. Everything is scoped to the caller's mailboxes.</summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/communications")]
public sealed class CommunicationsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>Images a browser renders without running anything; every other type is a download.</summary>
    private static readonly HashSet<string> InlineTypes =
        new(["image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Above the attachments' limit, so going over it is a 422 with a message rather than a dropped request.</summary>
    private const long SendRequestLimit = 40 * 1024 * 1024;

    private Guid UserId => userContextAccessor.Context.User!.Id;

    // ── Mailboxes ──

    [HttpGet("mailboxes")]
    public async Task<IActionResult> GetMailboxesAsync(CancellationToken ct) =>
        (await sender.Send(new GetMailboxesQuery(new GetMailboxesInput(UserId)), ct)).ToActionResult(errorMapper);

    /// <summary>Tests the connection first; a refused test is a 422 with the provider's message.</summary>
    [HttpPost("mailboxes")]
    public async Task<IActionResult> ConnectMailboxAsync(ConnectMailboxRequest body, CancellationToken ct) =>
        (await sender.Send(new ConnectMailboxCommand(new ConnectMailboxInput(
            UserId, body.Provider, body.ExternalAccountId, body.Address, body.DisplayName,
            body.ImapHost, body.ImapPort, body.SmtpHost, body.SmtpPort)), ct))
        .ToActionResult(errorMapper);

    [HttpPatch("mailboxes/{id:guid}")]
    public async Task<IActionResult> UpdateMailboxAsync(Guid id, UpdateMailboxRequest body, CancellationToken ct) =>
        (await sender.Send(new UpdateMailboxCommand(new UpdateMailboxInput(
            UserId, id, body.DisplayName, body.ImapHost, body.ImapPort, body.SmtpHost, body.SmtpPort)), ct))
        .ToActionResult(errorMapper);

    /// <summary>Deletes the local copy only; the credential stays in Integrations.</summary>
    [HttpDelete("mailboxes/{id:guid}")]
    public async Task<IActionResult> DeleteMailboxAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new DeleteMailboxCommand(new DeleteMailboxInput(UserId, id)), ct)).ToActionResult(errorMapper);

    /// <summary>One sync cycle, then the mailbox. A mailbox waiting for a reconnect is tested first.</summary>
    [HttpPost("mailboxes/{id:guid}/sync")]
    public async Task<IActionResult> SyncMailboxAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new SyncMailboxNowCommand(new SyncMailboxNowInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpGet("mailboxes/{id:guid}/labels")]
    public async Task<IActionResult> GetLabelsAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new GetLabelsQuery(new GetLabelsInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpPost("mailboxes/{id:guid}/labels")]
    public async Task<IActionResult> CreateLabelAsync(Guid id, LabelNameRequest body, CancellationToken ct) =>
        (await sender.Send(new CreateLabelCommand(new CreateLabelInput(UserId, id, body.Name)), ct)).ToActionResult(errorMapper);

    [HttpPatch("labels/{id:guid}")]
    public async Task<IActionResult> RenameLabelAsync(Guid id, LabelNameRequest body, CancellationToken ct) =>
        (await sender.Send(new RenameLabelCommand(new RenameLabelInput(UserId, id, body.Name)), ct)).ToActionResult(errorMapper);

    /// <summary>On IMAP only an empty folder can go, since what is inside goes with it (409 otherwise).</summary>
    [HttpDelete("labels/{id:guid}")]
    public async Task<IActionResult> DeleteLabelAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new DeleteLabelCommand(new DeleteLabelInput(UserId, id)), ct)).ToActionResult(errorMapper);

    // ── Messages ──

    /// <summary>One action on up to 100 messages of one mailbox; returns them as they are now.</summary>
    [HttpPost("messages/actions")]
    public async Task<IActionResult> ApplyMessageActionAsync(MessageActionRequest body, CancellationToken ct) =>
        (await sender.Send(new ApplyMessageActionCommand(new ApplyMessageActionInput(
            UserId, body.MailboxId, body.MessageIds ?? [], body.Action, body.LabelId)), ct))
        .ToActionResult(errorMapper);

    /// <summary>Newest first; <paramref name="before"/> is the previous page's <c>nextCursor</c>.</summary>
    [HttpGet("messages")]
    public async Task<IActionResult> ListMessagesAsync(
        [FromQuery] Guid? mailboxId, [FromQuery] Guid? labelId, [FromQuery] bool? unread, [FromQuery] string? q,
        [FromQuery] string? threadKey, [FromQuery] string? before, [FromQuery] int limit = 50, CancellationToken ct = default) =>
        (await sender.Send(new ListMessagesQuery(new ListMessagesInput(
            UserId, mailboxId, labelId, unread, q, threadKey, before, limit)), ct))
        .ToActionResult(errorMapper);

    /// <summary>The message with its body, fetched and cached on the first open.</summary>
    [HttpGet("messages/{id:guid}")]
    public async Task<IActionResult> OpenMessageAsync(Guid id, CancellationToken ct) =>
        (await sender.Send(new OpenMessageCommand(new OpenMessageInput(UserId, id)), ct)).ToActionResult(errorMapper);

    /// <summary>The form for a reply or a forward: <c>reply</c>, <c>reply-all</c> or <c>forward</c>.</summary>
    [HttpGet("messages/{id:guid}/compose")]
    public async Task<IActionResult> GetComposeDraftAsync(Guid id, [FromQuery] string? mode, CancellationToken ct) =>
        (await sender.Send(new GetComposeDraftQuery(new GetComposeDraftInput(UserId, id, mode)), ct)).ToActionResult(errorMapper);

    /// <summary>
    /// This cannot be taken back. A key already used is a 409. <c>SendUncertain</c> means it may have gone: the same key
    /// stays refused, and the screen says to look in Sent. <c>copySaved: false</c> is a success.
    /// </summary>
    [HttpPost("messages/send")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(SendRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = SendRequestLimit)]
    public async Task<IActionResult> SendMessageAsync(
        [FromForm] SendMessageRequest body, [FromForm] List<IFormFile>? files, CancellationToken ct)
    {
        var attachments = new List<OutgoingAttachment>();
        foreach (var file in files ?? [])
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            attachments.Add(new OutgoingAttachment(
                file.FileName, string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, buffer.ToArray()));
        }

        return (await sender.Send(new SendMessageCommand(new SendMessageInput(
            UserId, body.MailboxId, body.SendKey, body.To ?? [], body.Cc ?? [], body.Bcc ?? [], body.Subject, body.TextBody, body.HtmlBody,
            body.ReplyToId, body.ForwardOfId, body.KeepAttachmentIds ?? [], attachments)), ct))
            .ToActionResult(errorMapper);
    }

    [HttpGet("messages/{id:guid}/attachments/{attachmentId}")]
    public async Task<IActionResult> GetAttachmentAsync(Guid id, string attachmentId, CancellationToken ct)
    {
        var result = await sender.Send(new GetAttachmentQuery(new GetAttachmentInput(UserId, id, attachmentId)), ct);

        if (result.IsFailure)
        {
            var error = result.Errors[0];
            return StatusCode(errorMapper.MapToStatusCode(error.Type), errorMapper.Map(error));
        }

        var file = result.Value!;

        // The part comes from whoever sent the mail: only plain images render in place, and the browser
        // must not guess another type for them.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentDisposition =
            new ContentDisposition { Inline = InlineTypes.Contains(file.ContentType), FileName = file.FileName ?? "attachment" }.ToString();

        return File(file.Content, file.ContentType);
    }
}
