using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
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

        var (cards, numbers) = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var all = await ctx.AcquireRepository<ICardRepository>()
                .GetAllForUserAsync(input.UserId, input.IncludeArchived, token);

            IReadOnlyList<Card> selected = all;
            if (tagIds is { Count: > 0 })
            {
                // OR semantics: keep cards carrying any of the requested tags.
                var matching = (await ctx.AcquireRepository<ITagLinkRepository>()
                    .GetEntityIdsByTagsAsync(TaggableEntityType.Card, tagIds, token)).ToHashSet();
                selected = [.. all.Where(c => matching.Contains(c.Id))];
            }

            var numbers = await ctx.AcquireRepository<ICardNumberRepository>()
                .GetByCardsAsync([.. selected.Select(c => c.Id)], input.UserId, token);
            return (selected, numbers);
        }, cancellationToken: ct);

        var byCard = numbers.ToLookup(n => n.CardId);
        return Ok([.. cards.Select(c => CardDto.From(c, [.. byCard[c.Id]]))]);
    }
}
