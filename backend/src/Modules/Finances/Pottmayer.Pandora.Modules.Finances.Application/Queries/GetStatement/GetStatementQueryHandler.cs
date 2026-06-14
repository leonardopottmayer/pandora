using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Localization.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetStatement;

public sealed class GetStatementQueryHandler(IUnitOfWorkFactory factory, IMessageProvider messages)
    : QueryHandlerBase<GetStatementQuery, CardStatementDetailDto>
{
    protected override async Task<Result<CardStatementDetailDto>> HandleAsync(GetStatementQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var statement = await statements.FindByIdForUserAsync(input.StatementId, input.UserId, token);
            if (statement is null)
                return Result<CardStatementDetailDto>.Failure([StatementErrors.NotFound]);

            var transactions = await ctx.AcquireRepository<ITransactionRepository>().GetByStatementAsync(statement.Id, input.UserId, token);
            var dto = new CardStatementDetailDto(CardStatementDto.From(statement), [.. transactions.Select(t => TransactionDto.From(t, messages))]);
            return Result<CardStatementDetailDto>.Success(dto);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
