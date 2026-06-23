using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// Polymorphic link between a <see cref="Tag"/> and any module entity (fin005). The target is
/// addressed by <see cref="EntityType"/> + <see cref="EntityId"/> with no physical FK; that the
/// target exists and belongs to the owner is enforced in the application before a link is created.
/// The trio (tag, type, id) is unique. Links are immutable — created and removed, never edited.
/// </summary>
public sealed class TagLink : AggregateRoot<Guid>, IAuditable
{
    public Guid TagId { get; private set; }
    public TaggableEntityType EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private TagLink() { }

    /// <summary>Attaches a tag to an entity. That the entity exists and is owned by the user is enforced by the caller.</summary>
    public static TagLink Create(Guid tagId, TaggableEntityType entityType, Guid entityId, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TagId = tagId,
            EntityType = entityType,
            EntityId = entityId,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
