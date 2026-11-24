using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

/// <summary>
/// An asset class, system or the user's own. <see cref="Code"/> is set on system classes, which the UI
/// translates; only the user's own can be archived.
/// </summary>
public sealed record AssetClassDto(Guid Id, string? Code, string Name, string Behavior, bool IsSystem, DateTimeOffset? ArchivedAt)
{
    public static AssetClassDto From(SystemAssetClass c) => new(c.Id, c.Code, c.Name, c.Behavior.Value, true, null);

    public static AssetClassDto From(UserAssetClass c) => new(c.Id, null, c.Name, c.Behavior.Value, false, c.ArchivedAt);
}
