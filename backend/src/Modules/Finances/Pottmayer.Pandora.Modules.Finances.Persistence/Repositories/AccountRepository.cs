using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class AccountRepository(IDataContextAccessor accessor)
    : StandardRepository<Account, Guid>(accessor), IAccountRepository
{
    public Task<Account?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

    public async Task<IReadOnlyList<Account>> GetAllForUserAsync(
        Guid userId, bool includeArchived, CancellationToken ct = default)
    {
        var query = Queryable().Where(a => a.UserId == userId);

        if (!includeArchived)
            query = query.Where(a => a.ArchivedAt == null);

        return await query
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        var query = Queryable().Where(a => a.UserId == userId && a.Name.ToLower() == normalized);

        if (excludingId is not null)
            query = query.Where(a => a.Id != excludingId);

        return query.AnyAsync(ct);
    }
}
