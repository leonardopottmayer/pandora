namespace Pottmayer.Pandora.Modules.Admin.Domain.Entities;

/// <summary>A flag an administrator switched away from its default. Flags left at their default have no row.</summary>
public sealed class FeatureFlagOverride
{
    public string Key { get; private set; } = default!;
    public bool Enabled { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private FeatureFlagOverride() { }

    public static FeatureFlagOverride Create(string key, bool enabled, Guid actorId, TimeProvider timeProvider) =>
        new() { Key = key, Enabled = enabled, UpdatedBy = actorId, UpdatedAt = timeProvider.GetUtcNow() };

    public void Set(bool enabled, Guid actorId, TimeProvider timeProvider)
    {
        Enabled = enabled;
        UpdatedBy = actorId;
        UpdatedAt = timeProvider.GetUtcNow();
    }
}
