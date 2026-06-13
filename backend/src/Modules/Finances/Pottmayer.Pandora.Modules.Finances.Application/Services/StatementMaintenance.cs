using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

internal static class StatementMaintenance
{
    public sealed record StatementResolutionResult(CardStatement Statement, bool Created);

    public static async Task<Result<StatementResolutionResult>> EnsureStatementForPurchaseAsync(
        ICardStatementRepository statements,
        IStatementResolver resolver,
        Card card,
        Guid userId,
        DateOnly purchaseDate,
        Guid? forcedStatementId,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        if (forcedStatementId is not null)
        {
            var forced = await statements.FindByIdForUserAsync(forcedStatementId.Value, userId, ct);
            if (forced is null)
                return Result<StatementResolutionResult>.Failure([StatementErrors.NotFound]);
            if (forced.CardId != card.Id)
                return Result<StatementResolutionResult>.Failure([StatementErrors.ForcedStatementDoesNotBelongToCard]);
            if (forced.IsClosedToNewPurchases)
                return await EnsureNextStatementAsync(statements, resolver, card, userId, forced.ClosingDate.AddDays(1), timeProvider, ct);

            return Result<StatementResolutionResult>.Success(new StatementResolutionResult(forced, Created: false));
        }

        return await EnsureNextStatementAsync(statements, resolver, card, userId, purchaseDate, timeProvider, ct);
    }

    public static async Task SyncAsync(
        CardStatement statement,
        ICardStatementRepository statements,
        ITransactionRepository transactions,
        DateOnly today,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var total = await transactions.GetStatementTotalAsync(statement.Id, statement.UserId, ct);
        var paid = await transactions.GetStatementPaidTotalAsync(statement.Id, statement.UserId, ct);
        statement.SyncAmounts(total, paid, today, timeProvider);
        await statements.UpdateAsync(statement, ct);
    }

    private static async Task<Result<StatementResolutionResult>> EnsureNextStatementAsync(
        ICardStatementRepository statements,
        IStatementResolver resolver,
        Card card,
        Guid userId,
        DateOnly purchaseDate,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var resolution = resolver.Resolve(card, purchaseDate);
        var statement = await statements.FindByCardAndReferenceMonthAsync(card.Id, userId, resolution.ReferenceMonth, ct);
        if (statement is null)
        {
            statement = CardStatement.Create(
                userId,
                card.Id,
                resolution.ReferenceMonth,
                resolution.ClosingDate,
                resolution.DueDate,
                timeProvider);
            await statements.AddAsync(statement, ct);
            return Result<StatementResolutionResult>.Success(new StatementResolutionResult(statement, Created: true));
        }

        if (!statement.IsClosedToNewPurchases)
            return Result<StatementResolutionResult>.Success(new StatementResolutionResult(statement, Created: false));

        var nextResolution = resolver.Resolve(card, statement.ClosingDate.AddDays(1));
        var next = await statements.FindByCardAndReferenceMonthAsync(card.Id, userId, nextResolution.ReferenceMonth, ct);
        if (next is null)
        {
            next = CardStatement.Create(
                userId,
                card.Id,
                nextResolution.ReferenceMonth,
                nextResolution.ClosingDate,
                nextResolution.DueDate,
                timeProvider);
            await statements.AddAsync(next, ct);
            return Result<StatementResolutionResult>.Success(new StatementResolutionResult(next, Created: true));
        }

        return Result<StatementResolutionResult>.Success(new StatementResolutionResult(next, Created: false));
    }
}
