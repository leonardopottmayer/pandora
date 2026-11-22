using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class SpaceRepository(IDataContextAccessor accessor)
    : StandardRepository<Space, Guid>(accessor), ISpaceRepository
{
    public Task<Space?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && s.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Space>> GetForUserAsync(
        Guid userId, bool includeArchived, CancellationToken ct = default)
    {
        var query = Queryable().Where(s => s.UserId == userId && s.DeletedAt == null);
        if (!includeArchived)
            query = query.Where(s => s.ArchivedAt == null);

        return await query.OrderByDescending(s => s.IsDefault).ThenBy(s => s.Name).ToListAsync(ct);
    }

    public Task<Space?> FindDefaultAsync(Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(s => s.UserId == userId && s.IsDefault && s.DeletedAt == null, ct);
}
