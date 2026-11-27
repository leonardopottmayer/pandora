using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// An asset class the system ships (fin020): global, seeded, read-only, like a <see cref="SystemCategory"/>.
/// The user's own classes are <see cref="UserAssetClass"/>.
/// </summary>
public sealed class SystemAssetClass : AggregateRoot<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public AssetBehavior Behavior { get; private set; } = AssetBehavior.Unit;
    public int DisplayOrder { get; private set; }

    private SystemAssetClass() { }
}
