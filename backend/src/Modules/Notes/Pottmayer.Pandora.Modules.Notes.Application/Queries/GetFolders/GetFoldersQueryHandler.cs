using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetFolders;

public sealed class GetFoldersQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetFoldersQuery, IReadOnlyList<FolderDto>>
{
    protected override async Task<Result<IReadOnlyList<FolderDto>>> HandleAsync(GetFoldersQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var dtos = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var folders = await ctx.AcquireRepository<IFolderRepository>().GetForUserAsync(input.UserId, input.SpaceId, token);
            IReadOnlyList<FolderDto> list = [.. folders.Select(FolderDto.From)];
            return list;
        }, cancellationToken: ct);
        return Ok(dtos);
    }
}
