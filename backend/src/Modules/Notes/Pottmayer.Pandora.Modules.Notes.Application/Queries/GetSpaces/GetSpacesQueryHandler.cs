using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetSpaces;

public sealed class GetSpacesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetSpacesQuery, IReadOnlyList<SpaceDto>>
{
    protected override async Task<Result<IReadOnlyList<SpaceDto>>> HandleAsync(GetSpacesQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var dtos = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var spaces = await ctx.AcquireRepository<ISpaceRepository>()
                .GetForUserAsync(input.UserId, input.IncludeArchived, token);
            var stats = await ctx.AcquireRepository<IPageRepository>().GetSpaceStatsAsync(input.UserId, token);

            IReadOnlyList<SpaceDto> list = [.. spaces.Select(s => SpaceDto.From(s, stats.GetValueOrDefault(s.Id)))];
            return list;
        }, cancellationToken: ct);

        return Ok(dtos);
    }
}
