using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>
/// Single place to adjust <see cref="CardStatement.TotalAmount"/>/<see cref="CardStatement.PaidAmount"/>
/// by a delta and recompute the derived status. Every handler that creates, voids, restores or
/// reverses a statement-linked transaction goes through this so the four call sites stay in sync.
/// </summary>
internal static class StatementAmountSync
{
    public static void Apply(
        CardStatement statement, decimal totalDelta, decimal paidDelta, DateOnly today, TimeProvider timeProvider) =>
        statement.SyncAmounts(statement.TotalAmount + totalDelta, statement.PaidAmount + paidDelta, today, timeProvider);
}
