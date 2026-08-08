using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageArchived;

public sealed class SetPageArchivedCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetPageArchivedCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(SetPageArchivedCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();

            var page = await repo.FindByIdForUserAsync(input.PageId, input.UserId, token);
            if (page is null)
                return Result<Page>.Failure([PageErrors.NotFound]);

            if (page.IsArchived == input.Archived)
                return Result<Page>.Success(page); // idempotent: no change

            if (input.Archived)
                page.Archive(timeProvider);
            else
                page.Unarchive();

            await repo.UpdateAsync(page, token);

            return Result<Page>.Success(page);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(PageDto.From(result.Value!));
    }
}
