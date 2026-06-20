using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record ImportLayoutDto(
    Guid Id,
    string LayoutCode,
    string Name,
    string? BankName,
    string FileFormat,
    string AccountType,
    bool IsSystemLayout,
    DateTimeOffset CreatedAt)
{
    public static ImportLayoutDto From(ImportLayout l) => new(
        l.Id,
        l.LayoutCode,
        l.Name,
        l.BankName,
        l.FileFormat,
        l.AccountType,
        l.IsSystemLayout,
        l.CreatedAt);
}
