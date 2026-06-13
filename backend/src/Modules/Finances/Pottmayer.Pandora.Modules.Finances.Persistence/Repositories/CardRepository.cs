using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class CardRepository(IDataContextAccessor accessor)
    : StandardRepository<Card, Guid>(accessor), ICardRepository
{
    public Task<Card?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public async Task<IReadOnlyList<Card>> GetAllForUserAsync(
        Guid userId, bool includeArchived, CancellationToken ct = default)
    {
        var query = Queryable().Where(c => c.UserId == userId);

        if (!includeArchived)
            query = query.Where(c => c.ArchivedAt == null);

        return await query.OrderBy(c => c.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Card>> GetAllActiveAsync(CancellationToken ct = default)
        => await Queryable()
            .Where(c => c.ArchivedAt == null)
            .OrderBy(c => c.UserId)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

    public Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        var query = Queryable().Where(c => c.UserId == userId && c.Name.ToLower() == normalized);

        if (excludingId is not null)
            query = query.Where(c => c.Id != excludingId);

        return query.AnyAsync(ct);
    }
}
