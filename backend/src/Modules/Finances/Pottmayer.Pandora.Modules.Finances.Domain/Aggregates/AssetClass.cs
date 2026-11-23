using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// What kind of investment an asset is (fin020). System classes (seeded, <see cref="UserId"/> null)
/// are read-only; a user's own classes can be renamed and archived. The <see cref="Behavior"/> is fixed
/// at creation, since the positions already opened depend on it.
/// </summary>
public sealed class AssetClass : AggregateRoot<Guid>, IAuditable
{
    public Guid? UserId { get; private set; }
    public string? Code { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AssetBehavior Behavior { get; private set; } = AssetBehavior.Unit;
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsSystem => UserId is null;
    public bool IsArchived => ArchivedAt is not null;

    private AssetClass() { }

    /// <summary>Creates one of the user's own classes.</summary>
    public static AssetClass Create(Guid userId, string name, AssetBehavior behavior, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            Behavior = behavior,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Renames a user class. Returns <c>false</c> for a system class or an archived one.</summary>
    public bool Rename(string name)
    {
        if (IsSystem || IsArchived) return false;
        Name = name.Trim();
        return true;
    }

    /// <summary>Retires a user class. Returns <c>false</c> for a system class.</summary>
    public bool Archive(TimeProvider timeProvider)
    {
        if (IsSystem) return false;
        ArchivedAt ??= timeProvider.GetUtcNow();
        return true;
    }

    /// <summary>Brings a user class back. Returns <c>false</c> for a system class.</summary>
    public bool Unarchive()
    {
        if (IsSystem) return false;
        ArchivedAt = null;
        return true;
    }
}
