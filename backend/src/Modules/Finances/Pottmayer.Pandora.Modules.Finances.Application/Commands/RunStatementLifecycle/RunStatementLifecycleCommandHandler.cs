using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunStatementLifecycle;

public sealed class RunStatementLifecycleCommandHandler(
    IUnitOfWorkFactory factory,
    IStatementResolver resolver,
    IEffectiveTimeZoneResolver timeZones,
    TimeProvider timeProvider)
    : CommandHandlerBase<RunStatementLifecycleCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(RunStatementLifecycleCommand request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var days = new UserDays(timeZones, now, request.Input.Today);

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var cards = ctx.AcquireRepository<ICardRepository>();
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var transactions = ctx.AcquireRepository<ITransactionRepository>();

            var processed = 0;
            var activeCards = await cards.GetAllActiveAsync(token);

            // Ensures every active card always has both its current and next billing cycle open, so
            // purchases never have nowhere to land.
            foreach (var card in activeCards)
            {
                var today = await days.TodayAsync(card.UserId, token);
                var current = resolver.Resolve(card, today);
                if (await statements.FindByCardAndReferenceMonthAsync(card.Id, card.UserId, current.ReferenceMonth, token) is null)
                {
                    var statement = Pottmayer.Pandora.Modules.Finances.Domain.Aggregates.CardStatement.Create(
                        card.UserId, card.Id, current.ReferenceMonth, current.ClosingDate, current.DueDate, timeProvider);
                    await statements.AddAsync(statement, token);
                    await ctx.RecordAsync(card.UserId, card.UserId, StatementEvents.EntityType, statement.Id, StatementEvents.Created, now, new
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
                    await ctx.RecordAsync(card.UserId, card.UserId, StatementEvents.EntityType, statement.Id, StatementEvents.Created, now, new
                    {
                        statement.CardId,
                        statement.ReferenceMonth,
                        statement.ClosingDate,
                        statement.DueDate
                    }, ct: token);
                    processed++;
                }
            }

            // Candidates are statements past their closing or due date that still need a status
            // transition (close, mark paid/partially-paid, or flag overdue).
            // Fetched up to the latest day any user can be on, then held to each owner's own day.
            var lifecycleCandidates = await statements.GetLifecycleCandidatesAsync(days.Latest, token);
            foreach (var statement in lifecycleCandidates)
            {
                var today = await days.TodayAsync(statement.UserId, token);
                var due = (statement.Status == StatementStatus.Open && statement.ClosingDate <= today)
                    || (statement.Status != StatementStatus.Paid && statement.Status != StatementStatus.Overdue && statement.DueDate < today);
                if (!due)
                    continue;

                var changed = false;
                if (statement.Close(timeProvider))
                {
                    await ctx.RecordAsync(statement.UserId, statement.UserId, StatementEvents.EntityType, statement.Id, StatementEvents.Closed, now, ct: token);
                    changed = true;
                }

                // Re-deriving the status from the current amounts/date is what actually decides
                // paid/partially-paid/overdue — closing alone doesn't.
                var previousStatus = statement.Status.Value;
                statement.SyncAmounts(statement.TotalAmount, statement.PaidAmount, today, timeProvider);
                await statements.UpdateAsync(statement, token);
                if (statement.Status.Value == "overdue")
                {
                    // Only emit the event the moment it newly becomes overdue, not on every run after.
                    if (previousStatus != "overdue")
                    {
                        await ctx.RecordAsync(statement.UserId, statement.UserId, StatementEvents.EntityType, statement.Id, StatementEvents.Overdue, now, ct: token);
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
