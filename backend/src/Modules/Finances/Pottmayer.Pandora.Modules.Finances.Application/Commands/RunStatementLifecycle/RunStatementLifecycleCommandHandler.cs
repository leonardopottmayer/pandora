using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunStatementLifecycle;

public sealed class RunStatementLifecycleCommandHandler(
    IUnitOfWorkFactory factory,
    IStatementResolver resolver,
    TimeProvider timeProvider)
    : CommandHandlerBase<RunStatementLifecycleCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(RunStatementLifecycleCommand request, CancellationToken ct)
    {
        var today = request.Input.Today;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var cards = ctx.AcquireRepository<ICardRepository>();
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var transactions = ctx.AcquireRepository<ITransactionRepository>();

            var processed = 0;
            var activeCards = await cards.GetAllActiveAsync(token);

            foreach (var card in activeCards)
            {
                var current = resolver.Resolve(card, today);
                if (await statements.FindByCardAndReferenceMonthAsync(card.Id, card.UserId, current.ReferenceMonth, token) is null)
                {
                    var statement = Pottmayer.Pandora.Modules.Finances.Domain.Aggregates.CardStatement.Create(
                        card.UserId, card.Id, current.ReferenceMonth, current.ClosingDate, current.DueDate, timeProvider);
                    await statements.AddAsync(statement, token);
                    await ctx.RecordAsync(card.UserId, card.UserId, "statement", statement.Id, "statement.created", now, new
                    {
                        statement.CardId,
                        statement.ReferenceMonth,
                        statement.ClosingDate,
                        statement.DueDate
                    }, ct: token);
                    processed++;
                }

                var next = resolver.Resolve(card, today.AddMonths(1));
                if (await statements.FindByCardAndReferenceMonthAsync(card.Id, card.UserId, next.ReferenceMonth, token) is null)
                {
                    var statement = Pottmayer.Pandora.Modules.Finances.Domain.Aggregates.CardStatement.Create(
                        card.UserId, card.Id, next.ReferenceMonth, next.ClosingDate, next.DueDate, timeProvider);
                    await statements.AddAsync(statement, token);
                    await ctx.RecordAsync(card.UserId, card.UserId, "statement", statement.Id, "statement.created", now, new
                    {
                        statement.CardId,
                        statement.ReferenceMonth,
                        statement.ClosingDate,
                        statement.DueDate
                    }, ct: token);
                    processed++;
                }
            }

            var lifecycleCandidates = await statements.GetLifecycleCandidatesAsync(today, token);
            foreach (var statement in lifecycleCandidates)
            {
                var changed = false;
                if (statement.Close(timeProvider))
                {
                    await ctx.RecordAsync(statement.UserId, statement.UserId, "statement", statement.Id, "statement.closed", now, ct: token);
                    changed = true;
                }

                var previousStatus = statement.Status.Value;
                statement.SyncAmounts(statement.TotalAmount, statement.PaidAmount, today, timeProvider);
                await statements.UpdateAsync(statement, token);
                if (statement.Status.Value == "overdue")
                {
                    if (previousStatus != "overdue")
                    {
                        await ctx.RecordAsync(statement.UserId, statement.UserId, "statement", statement.Id, "statement.overdue", now, ct: token);
                        changed = true;
                    }
                }

                if (changed)
                    processed++;
            }

            return Result<int>.Success(processed);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }
}
