using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransactions;

public sealed class GetTransactionsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetTransactionsQuery, IReadOnlyList<TransactionDto>>
{
    private const int MaxTake = 200;

    protected override async Task<Result<IReadOnlyList<TransactionDto>>> HandleAsync(
        GetTransactionsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var take = input.Take is <= 0 or > MaxTake ? 50 : input.Take;
        var skip = input.Skip < 0 ? 0 : input.Skip;

        var filter = new TransactionFilter(
            input.AccountId, null, input.From, input.To, input.Kind, input.Status,
            input.SystemCategoryId, input.UserCategoryId, input.Text, input.Origin, skip, take);

        var transactions = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ITransactionRepository>();
            return await repo.QueryAsync(input.UserId, filter, token);
        }, cancellationToken: ct);

        IReadOnlyList<TransactionDto> dtos = [.. transactions.Select(TransactionDto.From)];
        return Ok(dtos);
    }
}
