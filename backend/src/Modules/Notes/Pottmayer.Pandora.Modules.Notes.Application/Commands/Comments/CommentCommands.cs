using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.Comments;

// Comments hang on a block of a page; a comment on a part of the text also carries the words it is
// about, and the editor marks that range with the thread's id. A comment whose block was removed
// stays, read in the page's list of comments. Comments are not part of the page's versions.

internal static class CommentRules
{
    public static bool BodyFits(string? body) => !string.IsNullOrWhiteSpace(body) && body.Trim().Length <= PageComment.MaxBody;
}

// ---------- List ----------

public sealed record GetPageCommentsInput(Guid UserId, Guid PageId);

/// <summary>A page's comments, threads and replies, oldest first.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetPageCommentsQuery(GetPageCommentsInput input) : QueryBase<GetPageCommentsInput, IReadOnlyList<CommentDto>>(input);

public sealed class GetPageCommentsQueryHandler(IUnitOfWorkFactory factory) : QueryHandlerBase<GetPageCommentsQuery, IReadOnlyList<CommentDto>>
{
    protected override async Task<Result<IReadOnlyList<CommentDto>>> HandleAsync(GetPageCommentsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            if (await ctx.AcquireRepository<IPageRepository>().FindByIdForUserAsync(input.PageId, input.UserId, token) is null)
                return Result<IReadOnlyList<CommentDto>>.Failure([PageErrors.NotFound]);
            var comments = await ctx.AcquireRepository<IPageCommentRepository>().GetByPageAsync(input.PageId, input.UserId, token);
            return Result<IReadOnlyList<CommentDto>>.Success([.. comments.Select(CommentDto.From)]);
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

// ---------- Start a thread ----------

public sealed record AddCommentInput(Guid UserId, Guid PageId, string BlockId, string? Quote, string Body);

[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class AddCommentCommand(AddCommentInput input) : CommandBase<AddCommentInput, CommentDto>(input);

public sealed class AddCommentCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider) : CommandHandlerBase<AddCommentCommand, CommentDto>
{
    protected override async Task<Result<CommentDto>> HandleAsync(AddCommentCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!CommentRules.BodyFits(input.Body))
            return Fail(CommentErrors.InvalidBody);
        if (string.IsNullOrWhiteSpace(input.BlockId) || input.BlockId.Length > PageComment.MaxBlockId || input.Quote?.Trim().Length > PageComment.MaxQuote)
            return Fail(CommentErrors.InvalidAnchor);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            if (await ctx.AcquireRepository<IPageRepository>().FindByIdForUserAsync(input.PageId, input.UserId, token) is null)
                return Result<CommentDto>.Failure([PageErrors.NotFound]);
            var comment = PageComment.Start(input.PageId, input.UserId, input.BlockId, input.Quote, input.Body, timeProvider);
            await ctx.AcquireRepository<IPageCommentRepository>().AddAsync(comment, token);
            return Result<CommentDto>.Success(CommentDto.From(comment));
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

// ---------- Reply ----------

public sealed record ReplyToCommentInput(Guid UserId, Guid ThreadId, string Body);

[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class ReplyToCommentCommand(ReplyToCommentInput input) : CommandBase<ReplyToCommentInput, CommentDto>(input);

public sealed class ReplyToCommentCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider) : CommandHandlerBase<ReplyToCommentCommand, CommentDto>
{
    protected override async Task<Result<CommentDto>> HandleAsync(ReplyToCommentCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!CommentRules.BodyFits(input.Body))
            return Fail(CommentErrors.InvalidBody);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageCommentRepository>();
            var thread = await repo.FindForUserAsync(input.ThreadId, input.UserId, token);
            if (thread is null)
                return Result<CommentDto>.Failure([CommentErrors.NotFound]);
            if (!thread.IsThread)
                return Result<CommentDto>.Failure([CommentErrors.NotAThread]);
            var reply = thread.Reply(input.Body, timeProvider);
            await repo.AddAsync(reply, token);
            return Result<CommentDto>.Success(CommentDto.From(reply));
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

// ---------- Edit, resolve, delete ----------

public sealed record EditCommentInput(Guid UserId, Guid CommentId, string Body);

[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class EditCommentCommand(EditCommentInput input) : CommandBase<EditCommentInput, CommentDto>(input);

public sealed class EditCommentCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider) : CommandHandlerBase<EditCommentCommand, CommentDto>
{
    protected override async Task<Result<CommentDto>> HandleAsync(EditCommentCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!CommentRules.BodyFits(input.Body))
            return Fail(CommentErrors.InvalidBody);
        var result = await CommentChange.RunAsync(factory, input.UserId, input.CommentId, c =>
        {
            c.Edit(input.Body, timeProvider);
            return null;
        }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

public sealed record SetCommentResolvedInput(Guid UserId, Guid CommentId, bool Resolved);

/// <summary>Marks a thread as resolved, which hides its highlight in the page, or opens it again.</summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class SetCommentResolvedCommand(SetCommentResolvedInput input) : CommandBase<SetCommentResolvedInput, CommentDto>(input);

public sealed class SetCommentResolvedCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider) : CommandHandlerBase<SetCommentResolvedCommand, CommentDto>
{
    protected override async Task<Result<CommentDto>> HandleAsync(SetCommentResolvedCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await CommentChange.RunAsync(factory, input.UserId, input.CommentId, c =>
        {
            if (!c.IsThread)
                return CommentErrors.NotAThread;
            c.SetResolved(input.Resolved, timeProvider);
            return null;
        }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}

internal static class CommentChange
{
    public static Task<Result<CommentDto>> RunAsync(
        IUnitOfWorkFactory factory, Guid userId, Guid commentId, Func<PageComment, Error?> change, CancellationToken ct)
        => factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageCommentRepository>();
            var comment = await repo.FindForUserAsync(commentId, userId, token);
            if (comment is null)
                return Result<CommentDto>.Failure([CommentErrors.NotFound]);
            if (change(comment) is { } error)
                return Result<CommentDto>.Failure([error]);
            await repo.UpdateAsync(comment, token);
            return Result<CommentDto>.Success(CommentDto.From(comment));
        }, cancellationToken: ct);
}

public sealed record DeleteCommentInput(Guid UserId, Guid CommentId);

/// <summary>Deletes a reply, or a whole thread with its replies.</summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class DeleteCommentCommand(DeleteCommentInput input) : CommandBase<DeleteCommentInput, bool>(input);

public sealed class DeleteCommentCommandHandler(IUnitOfWorkFactory factory) : CommandHandlerBase<DeleteCommentCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteCommentCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageCommentRepository>();
            var comment = await repo.FindForUserAsync(input.CommentId, input.UserId, token);
            if (comment is null)
                return Result<bool>.Failure([CommentErrors.NotFound]);
            if (comment.IsThread)
                await repo.DeleteRepliesAsync(comment.Id, token);
            await repo.RemoveAsync(comment, token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}

// ---------- Block dates ----------

public sealed record GetBlockStampsInput(Guid UserId, Guid PageId);

/// <summary>When each block of a page was written and last changed.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetBlockStampsQuery(GetBlockStampsInput input) : QueryBase<GetBlockStampsInput, IReadOnlyList<BlockStampDto>>(input);

public sealed class GetBlockStampsQueryHandler(IUnitOfWorkFactory factory) : QueryHandlerBase<GetBlockStampsQuery, IReadOnlyList<BlockStampDto>>
{
    protected override async Task<Result<IReadOnlyList<BlockStampDto>>> HandleAsync(GetBlockStampsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            if (await ctx.AcquireRepository<IPageRepository>().FindByIdForUserAsync(input.PageId, input.UserId, token) is null)
                return Result<IReadOnlyList<BlockStampDto>>.Failure([PageErrors.NotFound]);
            var stamps = await ctx.AcquireRepository<IBlockStampRepository>().GetByPageAsync(input.PageId, token);
            return Result<IReadOnlyList<BlockStampDto>>.Success([.. stamps.Select(BlockStampDto.From)]);
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
