using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardAvailableLimit;

public sealed class GetCardAvailableLimitQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetCardAvailableLimitQuery, CardAvailableLimitDto>
{
    protected override async Task<Result<CardAvailableLimitDto>> HandleAsync(
        GetCardAvailableLimitQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var card = await ctx.AcquireRepository<ICardRepository>().FindByIdForUserAsync(input.CardId, input.UserId, token);
            if (card is null)
                return Result<CardAvailableLimitDto>.Failure([CardErrors.NotFound]);

            decimal? available = null;
            if (card.CreditLimit is not null)
            {
                var unpaid = await ctx.AcquireRepository<ITransactionRepository>()
                    .GetUnpaidStatementTotalForCardAsync(card.Id, input.UserId, token);
                available = card.CreditLimit.Value - unpaid;
            }

            return Result<CardAvailableLimitDto>.Success(new CardAvailableLimitDto(card.Id, card.CreditLimit, available));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
