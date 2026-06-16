using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ReopenStatement;

public sealed class ReopenStatementCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<ReopenStatementCommand, CardStatementDto>
{
    protected override async Task<Result<CardStatementDto>> HandleAsync(ReopenStatementCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var statement = await statements.FindByIdForUserAsync(input.StatementId, input.UserId, token);
            if (statement is null)
                return Result<CardStatement>.Failure([StatementErrors.NotFound]);

            if (statement.Status == StatementStatus.Open)
                return Result<CardStatement>.Failure([StatementErrors.AlreadyOpen]);

            if (statement.Status == StatementStatus.Paid)
                return Result<CardStatement>.Failure([StatementErrors.AlreadyPaid]);

            statement.Reopen(today, timeProvider);
            await statements.UpdateAsync(statement, token);
            await ctx.RecordAsync(input.UserId, input.UserId, "statement", statement.Id, "statement.reopened", now, ct: token);

            return Result<CardStatement>.Success(statement);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardStatementDto.From(result.Value!));
    }
}
