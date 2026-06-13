using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardStatements;

public sealed class GetCardStatementsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetCardStatementsQuery, IReadOnlyList<CardStatementDto>>
{
    protected override async Task<Result<IReadOnlyList<CardStatementDto>>> HandleAsync(
        GetCardStatementsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var cards = ctx.AcquireRepository<ICardRepository>();
            var card = await cards.FindByIdForUserAsync(input.CardId, input.UserId, token);
            if (card is null)
                return Result<IReadOnlyList<CardStatementDto>>.Failure([CardErrors.NotFound]);

            var statements = await ctx.AcquireRepository<ICardStatementRepository>().GetByCardAsync(input.CardId, input.UserId, token);
            return Result<IReadOnlyList<CardStatementDto>>.Success([.. statements.Select(CardStatementDto.From)]);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
