using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

public sealed class StatementResolver : IStatementResolver
{
    public StatementResolution Resolve(Card card, DateOnly purchaseDate)
    {
        var referenceDate = purchaseDate.Day <= card.ClosingDay
            ? new DateOnly(purchaseDate.Year, purchaseDate.Month, 1)
            : new DateOnly(purchaseDate.Year, purchaseDate.Month, 1).AddMonths(1);

        var closingDate = new DateOnly(referenceDate.Year, referenceDate.Month, card.ClosingDay);
        var dueMonthBase = card.DueDay > card.ClosingDay ? referenceDate : referenceDate.AddMonths(1);
        var dueDate = new DateOnly(dueMonthBase.Year, dueMonthBase.Month, card.DueDay);
        var referenceMonth = $"{referenceDate.Year:D4}-{referenceDate.Month:D2}";

        return new StatementResolution(referenceMonth, closingDate, dueDate);
    }
}
