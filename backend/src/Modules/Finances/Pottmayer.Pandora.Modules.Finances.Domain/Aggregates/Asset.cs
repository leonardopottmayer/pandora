using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// Something the user owns (fin021): a stock, a CDB product, a coin. Its class is a system class or one
/// of the user's own, exactly one of the two (<see cref="SystemAssetClassId"/> / <see cref="UserAssetClassId"/>).
/// Its <see cref="Currency"/> must match the account of any position that holds it; like an account's,
/// it is fixed at creation. Its class can change only to one with the same behavior (<see cref="MoveTo"/>).
/// </summary>
public sealed class Asset : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid? SystemAssetClassId { get; private set; }
    public Guid? UserAssetClassId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Code { get; private set; }
    public string? Isin { get; private set; }
    public string? Issuer { get; private set; }
    public CurrencyCode Currency { get; private set; } = null!;
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;

    /// <summary>The asset's class, whichever table it lives in.</summary>
    public Guid AssetClassId => SystemAssetClassId ?? UserAssetClassId!.Value;

    private Asset() { }

    public static Asset Create(
        Guid userId, AssetClassRef assetClass, string name, string? code, string? isin, string? issuer,
        CurrencyCode currency, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SystemAssetClassId = assetClass.IsSystem ? assetClass.Id : null,
            UserAssetClassId = assetClass.IsSystem ? null : assetClass.Id,
            Name = name.Trim(),
            Code = NormalizeCode(code),
            Isin = NormalizeCode(isin),
            Issuer = Normalize(issuer),
            Currency = currency,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>
    /// Moves the asset to another class. The caller checks the behavior matches: positions follow the
    /// behavior, not the class.
    /// </summary>
    public void MoveTo(AssetClassRef assetClass)
    {
        SystemAssetClassId = assetClass.IsSystem ? assetClass.Id : null;
        UserAssetClassId = assetClass.IsSystem ? null : assetClass.Id;
    }

    /// <summary>Edits the asset. The currency stays: positions depend on it.</summary>
    public void Update(string name, string? code, string? isin, string? issuer)
    {
        Name = name.Trim();
        Code = NormalizeCode(code);
        Isin = NormalizeCode(isin);
        Issuer = Normalize(issuer);
    }

    public void Archive(TimeProvider timeProvider) => ArchivedAt ??= timeProvider.GetUtcNow();

    public void Unarchive() => ArchivedAt = null;

    /// <summary>Tickers and ISINs are compared upper-case, without spaces around.</summary>
    public static string? NormalizeCode(string? value) => Normalize(value)?.ToUpperInvariant();

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
