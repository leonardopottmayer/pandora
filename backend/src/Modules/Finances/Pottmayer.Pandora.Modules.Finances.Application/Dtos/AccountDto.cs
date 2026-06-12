using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record AccountDto(
    Guid Id,
    string Name,
    string Type,
    string Currency,
    string? Institution,
    string? Description,
    string? Color,
    string? Icon,
    int DisplayOrder,
    DateTimeOffset? ArchivedAt)
{
    public static AccountDto From(Account a) =>
        new(a.Id, a.Name, a.Type.Value, a.Currency.Value, a.Institution, a.Description,
            a.Color, a.Icon, a.DisplayOrder, a.ArchivedAt);
}
