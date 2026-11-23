using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record CardNumberDto(
    Guid Id,
    Guid CardId,
    string Kind,
    string LastFour,
    string? Nickname,
    DateTimeOffset? ArchivedAt)
{
    public static CardNumberDto From(CardNumber number) =>
        new(number.Id, number.CardId, number.Kind.Value, number.LastFour, number.Nickname, number.ArchivedAt);
}
