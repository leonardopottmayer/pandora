using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

/// <summary>An asset class; <see cref="Code"/> is set on system classes, which the UI translates.</summary>
public sealed record AssetClassDto(Guid Id, string? Code, string Name, string Behavior, bool IsSystem, DateTimeOffset? ArchivedAt)
{
    public static AssetClassDto From(AssetClass c) => new(c.Id, c.Code, c.Name, c.Behavior.Value, c.IsSystem, c.ArchivedAt);
}
