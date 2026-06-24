using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CloseStatement;

public sealed class CloseStatementCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CloseStatementCommand, CardStatementDto>
{
    protected override async Task<Result<CardStatementDto>> HandleAsync(CloseStatementCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var statement = await statements.FindByIdForUserAsync(input.StatementId, input.UserId, token);
            if (statement is null)
                return Result<CardStatement>.Failure([StatementErrors.NotFound]);

            // Close() no-ops on a statement that isn't open; only record the event when it actually changed.
            if (statement.Close(timeProvider))
            {
                // Closing can immediately flip the status further (e.g. straight to paid/overdue)
                // depending on the amounts already on the statement, so resync right away.
                statement.SyncAmounts(statement.TotalAmount, statement.PaidAmount, today, timeProvider);
                await statements.UpdateAsync(statement, token);
                await ctx.RecordAsync(input.UserId, input.UserId, StatementEvents.EntityType, statement.Id, StatementEvents.Closed, now, ct: token);
            }

            return Result<CardStatement>.Success(statement);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardStatementDto.From(result.Value!));
    }
}
