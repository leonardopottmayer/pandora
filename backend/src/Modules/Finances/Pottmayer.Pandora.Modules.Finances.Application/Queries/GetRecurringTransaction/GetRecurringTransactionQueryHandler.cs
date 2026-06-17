using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetRecurringTransaction;

public sealed class GetRecurringTransactionQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetRecurringTransactionQuery, RecurringTransactionDto>
{
    protected override async Task<Result<RecurringTransactionDto>> HandleAsync(
        GetRecurringTransactionQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var recurring = await repo.FindByIdForUserAsync(input.Id, input.UserId, token);
            if (recurring is null) return Result<RecurringTransactionDto>.Failure([RecurringTransactionErrors.NotFound]);
            return Result<RecurringTransactionDto>.Success(RecurringTransactionDto.From(recurring));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
