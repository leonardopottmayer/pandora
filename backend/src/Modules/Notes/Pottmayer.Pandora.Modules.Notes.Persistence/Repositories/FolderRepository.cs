using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class FolderRepository(IDataContextAccessor accessor)
    : StandardRepository<Folder, Guid>(accessor), IFolderRepository
{
    public Task<Folder?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId && f.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Folder>> GetForUserAsync(Guid userId, Guid? spaceId = null, CancellationToken ct = default)
    {
        var query = Queryable().Where(f => f.UserId == userId && f.DeletedAt == null);
        if (spaceId is { } space)
            query = query.Where(f => f.SpaceId == space);
        return await query.OrderBy(f => f.ParentId).ThenBy(f => f.OrderIndex).ThenBy(f => f.Name).ToListAsync(ct);
    }

    public Task<bool> AnyInSpaceAsync(Guid spaceId, CancellationToken ct = default)
        => Queryable().AnyAsync(f => f.SpaceId == spaceId && f.DeletedAt == null, ct);
}
