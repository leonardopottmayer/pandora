using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class TagRepository(IDataContextAccessor accessor)
    : StandardRepository<Tag, Guid>(accessor), ITagRepository
{
    public Task<Tag?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

    public async Task<IReadOnlyList<Tag>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable().Where(t => t.UserId == userId).OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Tag>> FindBySlugsAsync(
        Guid userId, IReadOnlyCollection<string> slugs, CancellationToken ct = default)
    {
        if (slugs.Count == 0)
            return [];

        return await Queryable()
            .Where(t => t.UserId == userId && slugs.Contains(t.Slug))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Tag>> GetByIdsForUserAsync(
        IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await Queryable()
            .Where(t => t.UserId == userId && ids.Contains(t.Id))
            .ToListAsync(ct);
    }
}
