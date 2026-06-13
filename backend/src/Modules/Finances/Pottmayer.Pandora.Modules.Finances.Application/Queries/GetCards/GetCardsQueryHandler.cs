using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
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
        var cards = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            return await ctx.AcquireRepository<ICardRepository>().GetAllForUserAsync(input.UserId, input.IncludeArchived, token);
        }, cancellationToken: ct);

        return Ok([.. cards.Select(CardDto.From)]);
    }
}
