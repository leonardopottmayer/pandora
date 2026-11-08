using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.Comments;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

public sealed record AddCommentRequest(string BlockId, string? Quote, string Body);
public sealed record CommentBodyRequest(string Body);
public sealed record SetCommentResolvedRequest(bool Resolved);

/// <summary>Comments on the blocks of a page, and when each block was written.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes")]
public sealed class CommentsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet("pages/{id:guid}/comments")]
    public async Task<IActionResult> ListAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new GetPageCommentsQuery(new GetPageCommentsInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpPost("pages/{id:guid}/comments")]
    public async Task<IActionResult> AddAsync(Guid id, AddCommentRequest request, CancellationToken ct)
        => (await sender.Send(new AddCommentCommand(new AddCommentInput(UserId, id, request.BlockId, request.Quote, request.Body)), ct)).ToActionResult(errorMapper);

    [HttpPost("comments/{id:guid}/replies")]
    public async Task<IActionResult> ReplyAsync(Guid id, CommentBodyRequest request, CancellationToken ct)
        => (await sender.Send(new ReplyToCommentCommand(new ReplyToCommentInput(UserId, id, request.Body)), ct)).ToActionResult(errorMapper);

    [HttpPut("comments/{id:guid}")]
    public async Task<IActionResult> EditAsync(Guid id, CommentBodyRequest request, CancellationToken ct)
        => (await sender.Send(new EditCommentCommand(new EditCommentInput(UserId, id, request.Body)), ct)).ToActionResult(errorMapper);

    [HttpPut("comments/{id:guid}/resolved")]
    public async Task<IActionResult> ResolveAsync(Guid id, SetCommentResolvedRequest request, CancellationToken ct)
        => (await sender.Send(new SetCommentResolvedCommand(new SetCommentResolvedInput(UserId, id, request.Resolved)), ct)).ToActionResult(errorMapper);

    /// <summary>Deletes a reply, or a thread with its replies.</summary>
    [HttpDelete("comments/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new DeleteCommentCommand(new DeleteCommentInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpGet("pages/{id:guid}/block-stamps")]
    public async Task<IActionResult> BlockStampsAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new GetBlockStampsQuery(new GetBlockStampsInput(UserId, id)), ct)).ToActionResult(errorMapper);
}
