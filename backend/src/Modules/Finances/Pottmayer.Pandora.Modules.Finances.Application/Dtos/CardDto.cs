using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record CardDto(
    Guid Id,
    string Name,
    string? Brand,
    // The active physical number's last four (else the first active one's): what names the card in lists.
    string? LastFour,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    string Currency,
    Guid AccountId,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<CardNumberDto> Numbers)
{
    /// <param name="numbers">The card's numbers, archived ones included, physical first.</param>
    public static CardDto From(Card card, IReadOnlyList<CardNumber> numbers)
    {
        var active = numbers.Where(n => !n.IsArchived).ToList();
        var main = active.FirstOrDefault(n => n.Kind == CardNumberKind.Physical) ?? active.FirstOrDefault();

        return new(
            card.Id,
            card.Name,
            card.Brand,
            main?.LastFour,
            card.CreditLimit,
            card.ClosingDay,
            card.DueDay,
            card.Currency.Value,
            card.AccountId,
            card.ArchivedAt,
            [.. numbers.Select(CardNumberDto.From)]);
    }
}
