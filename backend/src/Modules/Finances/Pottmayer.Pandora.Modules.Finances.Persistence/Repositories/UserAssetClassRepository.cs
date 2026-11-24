using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class UserAssetClassRepository(IDataContextAccessor accessor)
    : StandardRepository<UserAssetClass, Guid>(accessor), IUserAssetClassRepository
{
    public Task<UserAssetClass?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public async Task<IReadOnlyList<UserAssetClass>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable().Where(c => c.UserId == userId).OrderBy(c => c.Name).ToListAsync(ct);

    public Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        var query = Queryable().Where(c => c.UserId == userId && c.Name == trimmed);
        if (excludingId is not null) query = query.Where(c => c.Id != excludingId);
        return query.AnyAsync(ct);
    }
}
