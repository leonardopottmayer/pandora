using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTagLinks;

public sealed class GetTagLinksQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetTagLinksQuery, IReadOnlyList<TagLinkDto>>
{
    protected override async Task<Result<IReadOnlyList<TagLinkDto>>> HandleAsync(
        GetTagLinksQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var tag = await ctx.AcquireRepository<ITagRepository>().FindByIdForUserAsync(input.TagId, input.UserId, token);
            if (tag is null)
                return Result<IReadOnlyList<TagLink>>.Failure([TagErrors.NotFound]);

            var links = await ctx.AcquireRepository<ITagLinkRepository>().GetByTagAsync(tag.Id, token);
            return Result<IReadOnlyList<TagLink>>.Success(links);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok((IReadOnlyList<TagLinkDto>)[.. result.Value!.Select(TagLinkDto.From)]);
    }
}
