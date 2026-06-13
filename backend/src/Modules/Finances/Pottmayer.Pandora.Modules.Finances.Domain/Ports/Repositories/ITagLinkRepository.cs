using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface ITagLinkRepository : IStandardRepository<TagLink, Guid>
{
    /// <summary>The single link for a (tag, type, id) trio, or <c>null</c>.</summary>
    Task<TagLink?> FindAsync(Guid tagId, TaggableEntityType entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>All links of one tag.</summary>
    Task<IReadOnlyList<TagLink>> GetByTagAsync(Guid tagId, CancellationToken ct = default);

    /// <summary>All links attached to a single entity.</summary>
    Task<IReadOnlyList<TagLink>> GetByEntityAsync(TaggableEntityType entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>
    /// Distinct ids of entities of the given type that carry <em>any</em> of the supplied tags (OR
    /// semantics) — the set used to filter listings by tag.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetEntityIdsByTagsAsync(
        TaggableEntityType entityType, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default);
}
