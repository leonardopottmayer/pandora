using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTags;

public sealed class GetTagsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetTagsQuery, IReadOnlyList<TagDto>>
{
    protected override async Task<Result<IReadOnlyList<TagDto>>> HandleAsync(GetTagsQuery request, CancellationToken ct)
    {
        var tags = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            return await ctx.AcquireRepository<ITagRepository>().GetAllForUserAsync(request.Input.UserId, token);
        }, cancellationToken: ct);

        return Ok([.. tags.Select(TagDto.From)]);
    }
}
