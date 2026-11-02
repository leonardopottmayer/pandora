using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.MovePage;

public sealed class MovePageCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<MovePageCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(MovePageCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();

            var page = await repo.FindByIdForUserAsync(input.PageId, input.UserId, token);
            if (page is null)
                return Result<PageDto>.Failure([PageErrors.NotFound]);

            var spaceId = page.SpaceId;
            if (input.ParentId is { } parentId)
            {
                var parent = await repo.FindByIdForUserAsync(parentId, input.UserId, token);
                if (parent is null)
                    return Result<PageDto>.Failure([PageErrors.ParentNotFound]);

                var parents = await repo.GetParentMapForUserAsync(input.UserId, token);
                if (PageHierarchy.WouldCreateCycle(page.Id, parentId, parents))
                    return Result<PageDto>.Failure([PageErrors.CycleDetected]);

                spaceId = parent.SpaceId;
            }
            else if (input.SpaceId is { } targetSpaceId)
            {
                if (await ctx.AcquireRepository<ISpaceRepository>().FindByIdForUserAsync(targetSpaceId, input.UserId, token) is null)
                    return Result<PageDto>.Failure([SpaceErrors.NotFound]);
                spaceId = targetSpaceId;
            }

            page.Move(input.ParentId, input.OrderIndex);

            // A page never sits in a different space from its parent, so its descendants come along.
            if (spaceId != page.SpaceId)
            {
                var all = await repo.GetTreeForUserAsync(input.UserId, includeArchived: true, token);
                foreach (var descendant in PageHierarchy.Subtree(page.Id, all).Where(p => p.Id != page.Id))
                {
                    descendant.MoveToSpace(spaceId);
                    await repo.UpdateAsync(descendant, token);
                }
                page.MoveToSpace(spaceId);
            }

            await repo.UpdateAsync(page, token);

            // The tags did not change here, but the page view carries them.
            return Result<PageDto>.Success(
                PageDto.From(page, await PageTagReader.LoadAsync(ctx, page.Id, input.UserId, token)));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
