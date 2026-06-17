using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPendingTransactions;

public sealed class GetPendingTransactionsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPendingTransactionsQuery, IReadOnlyList<PendingTransactionDto>>
{
    protected override async Task<Result<IReadOnlyList<PendingTransactionDto>>> HandleAsync(
        GetPendingTransactionsQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPendingTransactionRepository>();
            var items = await repo.QueryAsync(input.UserId, input.Filter, token);
            IReadOnlyList<PendingTransactionDto> dtos = items.Select(PendingTransactionDto.From).ToList();
            return Result<IReadOnlyList<PendingTransactionDto>>.Success(dtos);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
