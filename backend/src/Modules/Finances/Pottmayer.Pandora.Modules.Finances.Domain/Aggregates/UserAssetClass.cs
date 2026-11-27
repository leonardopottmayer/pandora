using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// One of the user's own asset classes (fin025), for what the system list does not have, like a
/// <see cref="UserCategory"/>. It can be renamed and archived; its <see cref="Behavior"/> is fixed at
/// creation, since the positions already opened depend on it.
/// </summary>
public sealed class UserAssetClass : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AssetBehavior Behavior { get; private set; } = AssetBehavior.Unit;
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;

    private UserAssetClass() { }

    public static UserAssetClass Create(Guid userId, string name, AssetBehavior behavior, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            Behavior = behavior,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Renames the class. Returns <c>false</c> while it is archived.</summary>
    public bool Rename(string name)
    {
        if (IsArchived) return false;
        Name = name.Trim();
        return true;
    }

    public void Archive(TimeProvider timeProvider) => ArchivedAt ??= timeProvider.GetUtcNow();

    public void Unarchive() => ArchivedAt = null;
}
