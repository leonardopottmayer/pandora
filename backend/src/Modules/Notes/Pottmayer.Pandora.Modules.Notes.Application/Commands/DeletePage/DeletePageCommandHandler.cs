using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.DeletePage;

public sealed class DeletePageCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeletePageCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeletePageCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();

            var page = await repo.FindByIdForUserAsync(input.PageId, input.UserId, token);
            if (page is null)
                return Result<bool>.Failure([PageErrors.NotFound]);

            var links = ctx.AcquireRepository<IPageLinkRepository>();
            var tags = ctx.AcquireRepository<ITagRepository>();
            var pageTags = ctx.AcquireRepository<IPageTagRepository>();

            // Soft-delete the whole subtree so no child is left pointing at a deleted parent.
            var all = await repo.GetTreeForUserAsync(input.UserId, includeArchived: true, token);
            foreach (var descendant in Subtree(page.Id, all))
            {
                descendant.Delete(timeProvider);
                await repo.UpdateAsync(descendant, token);

                // Edges leaving a deleted page are gone for good; edges pointing at it stay and are
                // filtered out on read, so restoring the row would restore its inbound mentions.
                await links.RemoveBySourceAsync(descendant.Id, token);

                // A deleted page stops carrying its tags, and a tag nobody carries anymore goes with
                // it unless it kept a color.
                await PageTagSynchronizer.ClearAsync(descendant.Id, input.UserId, tags, pageTags, token);
            }

            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }

    /// <summary>The page plus every descendant, found by walking the parent links breadth-first.</summary>
    private static IEnumerable<Page> Subtree(Guid rootId, IReadOnlyList<Page> all)
    {
        var byParent = all.ToLookup(p => p.ParentId);
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            var node = all.FirstOrDefault(p => p.Id == id);
            if (node is null)
                continue;

            yield return node;
            foreach (var child in byParent[id])
                queue.Enqueue(child.Id);
        }
    }
}
