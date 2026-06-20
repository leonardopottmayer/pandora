using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Localization.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransaction;

public sealed class GetTransactionQueryHandler(IUnitOfWorkFactory factory, IMessageProvider messages)
    : QueryHandlerBase<GetTransactionQuery, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        GetTransactionQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var tx = await ctx.AcquireRepository<ITransactionRepository>()
                .FindByIdForUserAsync(input.Id, input.UserId, token);
            if (tx is null) return Result<(Transaction, CardStatement?)>.Failure([TransactionErrors.NotFound]);

            CardStatement? statement = null;
            if (tx.CardStatementId.HasValue)
            {
                var stmts = await ctx.AcquireRepository<ICardStatementRepository>()
                    .GetByIdsAsync([tx.CardStatementId.Value], input.UserId, token);
                statement = stmts.FirstOrDefault();
            }

            return Result<(Transaction, CardStatement?)>.Success((tx, statement));
        }, cancellationToken: ct);

        if (result.IsFailure) return Fail([.. result.Errors]);

        var (transaction, stmt) = result.Value!;
        return Ok(TransactionDto.From(transaction, messages, stmt));
    }
}
