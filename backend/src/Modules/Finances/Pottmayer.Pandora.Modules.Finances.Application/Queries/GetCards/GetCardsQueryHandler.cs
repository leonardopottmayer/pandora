using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;

public sealed class GetCardsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetCardsQuery, IReadOnlyList<CardDto>>
{
    protected override async Task<Result<IReadOnlyList<CardDto>>> HandleAsync(GetCardsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var tagIds = input.TagIds?.Distinct().ToList();

        var cards = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var all = await ctx.AcquireRepository<ICardRepository>()
                .GetAllForUserAsync(input.UserId, input.IncludeArchived, token);

            if (tagIds is not { Count: > 0 })
                return all;

            // OR semantics: keep cards carrying any of the requested tags.
            var matching = (await ctx.AcquireRepository<ITagLinkRepository>()
                .GetEntityIdsByTagsAsync(TaggableEntityType.Card, tagIds, token)).ToHashSet();
            return [.. all.Where(c => matching.Contains(c.Id))];
        }, cancellationToken: ct);

        return Ok([.. cards.Select(CardDto.From)]);
    }
}
