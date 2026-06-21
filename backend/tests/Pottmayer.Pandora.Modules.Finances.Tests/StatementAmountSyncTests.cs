using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class StatementAmountSyncTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 6, 13);

    private static CardStatement NewStatement(TimeProvider time) =>
        CardStatement.Create(Guid.NewGuid(), Guid.NewGuid(), "2026-06", Today.AddDays(2), Today.AddDays(9), time);

    [Fact]
    public void Apply_adds_total_delta_and_keeps_open_while_unpaid()
    {
        var time = new FixedTimeProvider(Now);
        var statement = NewStatement(time);

        StatementAmountSync.Apply(statement, totalDelta: 100m, paidDelta: 0m, Today, time);

        Assert.Equal(100m, statement.TotalAmount);
        Assert.Equal(0m, statement.PaidAmount);
        Assert.Equal(100m, statement.RemainingAmount);
        Assert.Equal(StatementStatus.Open, statement.Status);
    }

    [Fact]
    public void Apply_accumulates_across_calls()
    {
        var time = new FixedTimeProvider(Now);
        var statement = NewStatement(time);

        StatementAmountSync.Apply(statement, 100m, 0m, Today, time);
        StatementAmountSync.Apply(statement, 50m, 0m, Today, time);   // another purchase
        StatementAmountSync.Apply(statement, 0m, 30m, Today, time);   // a partial payment

        Assert.Equal(150m, statement.TotalAmount);
        Assert.Equal(30m, statement.PaidAmount);
        Assert.Equal(120m, statement.RemainingAmount);
    }

    [Fact]
    public void Apply_marks_paid_when_payment_clears_the_balance()
    {
        var time = new FixedTimeProvider(Now);
        var statement = NewStatement(time);
        StatementAmountSync.Apply(statement, 100m, 0m, Today, time);

        StatementAmountSync.Apply(statement, 0m, 100m, Today, time);

        Assert.Equal(0m, statement.RemainingAmount);
        Assert.Equal(StatementStatus.Paid, statement.Status);
    }

    [Fact]
    public void Apply_can_reverse_a_charge_with_a_negative_delta()
    {
        var time = new FixedTimeProvider(Now);
        var statement = NewStatement(time);
        StatementAmountSync.Apply(statement, 100m, 0m, Today, time);

        // Voiding a transaction feeds back a negative total delta.
        StatementAmountSync.Apply(statement, -40m, 0m, Today, time);

        Assert.Equal(60m, statement.TotalAmount);
    }
}
