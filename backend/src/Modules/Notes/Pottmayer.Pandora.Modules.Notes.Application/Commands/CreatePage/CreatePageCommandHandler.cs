using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;

public sealed class CreatePageCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreatePageCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(CreatePageCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(PageErrors.InvalidTitle);

        if (!PageContent.TryParse(input.Content, out var content, out var problem))
            return Fail(PageErrors.InvalidContent(problem!));

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();

            // A child page must hang off one the user actually owns (404-on-foreign-resource rule),
            // and lives in its parent's space whatever space was asked for.
            Guid spaceId;
            if (input.ParentId is { } parentId)
            {
                var parent = await repo.FindByIdForUserAsync(parentId, input.UserId, token);
                if (parent is null)
                    return Result<PageDto>.Failure([PageErrors.ParentNotFound]);
                spaceId = parent.SpaceId;
            }
            else if (input.FolderId is { } folderId)
            {
                var folder = await ctx.AcquireRepository<IFolderRepository>().FindByIdForUserAsync(folderId, input.UserId, token);
                if (folder is null)
                    return Result<PageDto>.Failure([FolderErrors.NotFound]);
                spaceId = folder.SpaceId;
            }
            else
            {
                var space = await SpaceResolver.ResolveAsync(
                    ctx.AcquireRepository<ISpaceRepository>(), input.UserId, input.SpaceId, timeProvider, token);
                if (space.IsFailure)
                    return Result<PageDto>.Failure([.. space.Errors]);
                spaceId = space.Value!.Id;
            }

            var slug = await ResolveUniqueSlugAsync(repo, input.UserId, Slugger.From(input.Title), token);

            var page = Page.Create(
                input.UserId, spaceId, input.Title, slug, input.ParentId, input.Icon,
                orderIndex: 0, content, timeProvider, input.ParentId is null ? input.FolderId : null);
            page.SetHistorySource(input.Source);
            await repo.AddAsync(page, token);

            // A page may already carry page links and tags in its initial blocks (e.g. a duplicated note).
            var links = ctx.AcquireRepository<IPageLinkRepository>();
            await PageLinkSynchronizer.RebuildAsync(page, content, repo, links, timeProvider, token);
            await PageMediaSynchronizer.RebuildAsync(
                page, content, ctx.AcquireRepository<IMediaItemRepository>(), ctx.AcquireRepository<IPageMediaRepository>(),
                timeProvider, token);

            // And when each block was written, and by what.
            await BlockStampSynchronizer.RebuildAsync(page, content, ctx.AcquireRepository<IBlockStampRepository>(), timeProvider, token);

            var tags = await PageTagSynchronizer.RebuildAsync(
                page,
                content,
                ctx.AcquireRepository<ITagRepository>(),
                ctx.AcquireRepository<IPageTagRepository>(),
                timeProvider,
                token);

            return Result<PageDto>.Success(PageDto.From(page, tags));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }

    /// <summary>Appends "-2", "-3", … to the base slug until it is free for this user.</summary>
    private static async Task<string> ResolveUniqueSlugAsync(
        IPageRepository repo, Guid userId, string baseSlug, CancellationToken ct)
    {
        if (!await repo.ExistsWithSlugAsync(userId, baseSlug, ct))
            return baseSlug;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseSlug}-{suffix}";
            if (!await repo.ExistsWithSlugAsync(userId, candidate, ct))
                return candidate;
        }
    }
}
