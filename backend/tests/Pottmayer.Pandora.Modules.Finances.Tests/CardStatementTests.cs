using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class CardStatementTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Close_then_partial_then_paid_updates_status()
    {
        var time = new FixedTimeProvider(Now);
        var statement = CardStatement.Create(Guid.NewGuid(), Guid.NewGuid(), "2026-06", new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 20), time);

        Assert.True(statement.Close(time));
        statement.SyncAmounts(100m, 40m, new DateOnly(2026, 6, 12), time);
        Assert.Equal("partially-paid", statement.Status.Value);

        statement.SyncAmounts(100m, 100m, new DateOnly(2026, 6, 12), time);
        Assert.Equal("paid", statement.Status.Value);
        Assert.Equal(0m, statement.RemainingAmount);
    }

    [Fact]
    public void Past_due_statement_becomes_overdue_when_not_fully_paid()
    {
        var time = new FixedTimeProvider(Now);
        var statement = CardStatement.Create(Guid.NewGuid(), Guid.NewGuid(), "2026-06", new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 20), time);
        statement.Close(time);

        statement.SyncAmounts(100m, 20m, new DateOnly(2026, 6, 21), time);

        Assert.Equal("overdue", statement.Status.Value);
    }
}
