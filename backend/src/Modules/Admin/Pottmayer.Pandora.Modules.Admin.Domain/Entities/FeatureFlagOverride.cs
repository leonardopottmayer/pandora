namespace Pottmayer.Pandora.Modules.Admin.Domain.Entities;

/// <summary>A flag an administrator switched away from its default. Flags left at their default have no row.</summary>
public sealed class FeatureFlagOverride
{
    public string Key { get; private set; } = default!;
    public bool Enabled { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private FeatureFlagOverride() { }
}
