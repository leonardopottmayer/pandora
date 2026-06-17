using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetRecurringTransactionsQuery, IReadOnlyList<RecurringTransactionDto>>
{
    protected override async Task<Result<IReadOnlyList<RecurringTransactionDto>>> HandleAsync(
        GetRecurringTransactionsQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var items = await repo.GetAllForUserAsync(input.UserId, token);
            IReadOnlyList<RecurringTransactionDto> dtos = items.Select(RecurringTransactionDto.From).ToList();
            return Result<IReadOnlyList<RecurringTransactionDto>>.Success(dtos);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
