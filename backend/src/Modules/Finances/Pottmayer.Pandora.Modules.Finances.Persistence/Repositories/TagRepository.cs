using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class TagRepository(IDataContextAccessor accessor)
    : StandardRepository<Tag, Guid>(accessor), ITagRepository
{
    public Task<Tag?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

    public async Task<IReadOnlyList<Tag>> GetAllForUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Tag>> GetByIdsForUserAsync(
        Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await Queryable()
            .Where(t => t.UserId == userId && ids.Contains(t.Id))
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        var query = Queryable().Where(t => t.UserId == userId && t.Name.ToLower() == normalized);

        if (excludingId is not null)
            query = query.Where(t => t.Id != excludingId);

        return query.AnyAsync(ct);
    }
}
