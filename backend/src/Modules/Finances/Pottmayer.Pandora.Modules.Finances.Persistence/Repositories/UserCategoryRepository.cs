using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class UserCategoryRepository(IDataContextAccessor accessor)
    : StandardRepository<UserCategory, Guid>(accessor), IUserCategoryRepository
{
    public Task<UserCategory?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public async Task<IReadOnlyList<UserCategory>> GetAllForUserAsync(
        Guid userId, bool includeInactive, CancellationToken ct = default)
    {
        var query = Queryable().Where(c => c.UserId == userId);

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsWithNameAsync(
        Guid userId, string name, Guid? parentCategoryId, Guid? excludingId, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        var query = Queryable().Where(c =>
            c.UserId == userId &&
            c.Name.ToLower() == normalized);

        query = parentCategoryId is null
            ? query.Where(c => c.ParentCategoryId == null)
            : query.Where(c => c.ParentCategoryId == parentCategoryId);

        if (excludingId is not null)
            query = query.Where(c => c.Id != excludingId);

        return query.AnyAsync(ct);
    }
}
