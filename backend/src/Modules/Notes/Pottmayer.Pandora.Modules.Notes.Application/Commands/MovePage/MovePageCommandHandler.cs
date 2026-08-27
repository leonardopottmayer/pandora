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

            if (input.ParentId is { } parentId)
            {
                if (await repo.FindByIdForUserAsync(parentId, input.UserId, token) is null)
                    return Result<PageDto>.Failure([PageErrors.ParentNotFound]);

                var parents = await repo.GetParentMapForUserAsync(input.UserId, token);
                if (PageHierarchy.WouldCreateCycle(page.Id, parentId, parents))
                    return Result<PageDto>.Failure([PageErrors.CycleDetected]);
            }

            page.Move(input.ParentId, input.OrderIndex);
            await repo.UpdateAsync(page, token);

            // The tags did not change here, but the page view carries them.
            return Result<PageDto>.Success(
                PageDto.From(page, await PageTagReader.LoadAsync(ctx, page.Id, input.UserId, token)));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
