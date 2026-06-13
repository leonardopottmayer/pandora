using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record CardDto(
    Guid Id,
    string Name,
    string? Brand,
    string? LastFour,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    string Currency,
    Guid? DefaultPaymentAccountId,
    DateTimeOffset? ArchivedAt)
{
    public static CardDto From(Card card) =>
        new(
            card.Id,
            card.Name,
            card.Brand,
            card.LastFour,
            card.CreditLimit,
            card.ClosingDay,
            card.DueDay,
            card.Currency.Value,
            card.DefaultPaymentAccountId,
            card.ArchivedAt);
}
