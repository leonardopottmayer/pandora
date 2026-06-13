using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class StatementResolverTests
{
    private static Card NewCard(int closingDay, int dueDay) =>
        Card.Create(Guid.NewGuid(), "Card", null, null, null, closingDay, dueDay, CurrencyCode.Create("BRL"), null, new FixedTimeProvider(DateTimeOffset.UtcNow));

    [Fact]
    public void Purchase_on_closing_day_stays_in_current_month()
    {
        var resolver = new StatementResolver();
        var result = resolver.Resolve(NewCard(10, 20), new DateOnly(2026, 6, 10));

        Assert.Equal("2026-06", result.ReferenceMonth);
        Assert.Equal(new DateOnly(2026, 6, 10), result.ClosingDate);
        Assert.Equal(new DateOnly(2026, 6, 20), result.DueDate);
    }

    [Fact]
    public void Purchase_after_closing_day_moves_to_next_month_even_on_year_boundary()
    {
        var resolver = new StatementResolver();
        var result = resolver.Resolve(NewCard(28, 5), new DateOnly(2026, 12, 29));

        Assert.Equal("2027-01", result.ReferenceMonth);
        Assert.Equal(new DateOnly(2027, 1, 28), result.ClosingDate);
        Assert.Equal(new DateOnly(2027, 2, 5), result.DueDate);
    }
}
