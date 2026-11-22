using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A virtual folder of the media library (nte008): a name in a tree, nothing on disk. Deleting one
/// moves what it holds — subfolders and items — up a level first, so nothing is lost or left
/// pointing at a missing parent. Cycles on move are refused by the caller with
/// <see cref="PageHierarchy.WouldCreateCycle"/>, the same walk the page tree uses.
/// </summary>
public sealed class MediaFolder : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private MediaFolder() { }

    public static MediaFolder Create(Guid userId, Guid? parentId, string name, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ParentId = parentId,
            Name = name.Trim(),
            CreatedAt = timeProvider.GetUtcNow()
        };

    public void Rename(string name) => Name = name.Trim();

    public void MoveTo(Guid? parentId) => ParentId = parentId == Id ? null : parentId;
}
