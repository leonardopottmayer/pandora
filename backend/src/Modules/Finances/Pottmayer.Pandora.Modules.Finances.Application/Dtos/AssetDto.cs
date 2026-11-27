using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record AssetDto(
    Guid Id, Guid AssetClassId, string Name, string? Code, string? Isin, string? Issuer, string Currency, DateTimeOffset? ArchivedAt)
{
    public static AssetDto From(Asset a) => new(a.Id, a.AssetClassId, a.Name, a.Code, a.Isin, a.Issuer, a.Currency.Value, a.ArchivedAt);
}
