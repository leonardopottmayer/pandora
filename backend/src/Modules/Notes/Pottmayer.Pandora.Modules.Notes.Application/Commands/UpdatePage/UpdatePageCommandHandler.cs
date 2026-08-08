using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdatePage;

public sealed class UpdatePageCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UpdatePageCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(UpdatePageCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(PageErrors.InvalidTitle);

        var result = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();

            var page = await repo.FindByIdForUserAsync(input.PageId, input.UserId, token);
            if (page is null)
                return Result<Page>.Failure([PageErrors.NotFound]);

            page.Update(input.Title, input.Icon, input.ContentMarkdown);
            await repo.UpdateAsync(page, token);

            return Result<Page>.Success(page);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(PageDto.From(result.Value!));
    }
}
