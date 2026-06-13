using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCard;

public sealed class GetCardQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetCardQuery, CardDto>
{
    protected override async Task<Result<CardDto>> HandleAsync(GetCardQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var card = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            return await ctx.AcquireRepository<ICardRepository>().FindByIdForUserAsync(input.CardId, input.UserId, token);
        }, cancellationToken: ct);

        return card is null ? Fail(CardErrors.NotFound) : Ok(CardDto.From(card));
    }
}
