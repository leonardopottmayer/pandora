using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class TagLinkRepository(IDataContextAccessor accessor)
    : StandardRepository<TagLink, Guid>(accessor), ITagLinkRepository
{
    public Task<TagLink?> FindAsync(Guid tagId, TaggableEntityType entityType, Guid entityId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(
            l => l.TagId == tagId && l.EntityType == entityType && l.EntityId == entityId, ct);

    public async Task<IReadOnlyList<TagLink>> GetByTagAsync(Guid tagId, CancellationToken ct = default)
        => await Queryable().Where(l => l.TagId == tagId).ToListAsync(ct);

    public async Task<IReadOnlyList<TagLink>> GetByEntityAsync(
        TaggableEntityType entityType, Guid entityId, CancellationToken ct = default)
        => await Queryable()
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetEntityIdsByTagsAsync(
        TaggableEntityType entityType, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default)
    {
        if (tagIds.Count == 0)
            return [];

        return await Queryable()
            .Where(l => l.EntityType == entityType && tagIds.Contains(l.TagId))
            .Select(l => l.EntityId)
            .Distinct()
            .ToListAsync(ct);
    }
}
