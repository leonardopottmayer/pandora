using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.Repositories;

public sealed class UserRoleRepository(IDataContextAccessor accessor)
    : StandardRepository<UserRole, Guid>(accessor), IUserRoleRepository
{
    public Task<bool> ExistsAsync(Guid userId, Guid roleId, CancellationToken ct = default)
        => Queryable().AnyAsync(r => r.UserId == userId && r.RoleId == roleId, ct);

    public async Task<IReadOnlyList<string>> ListPatternsAsync(Guid userId, CancellationToken ct = default)
        => await Queryable(r => r.UserId == userId)
            .Join(DbContext.Set<RolePermission>(), r => r.RoleId, p => p.RoleId, (_, p) => p.Pattern)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserRole>> ListAllAsync(CancellationToken ct = default)
        => await Queryable().AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<UserRole>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable(r => r.UserId == userId).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListHolderIdsAsync(Guid roleId, CancellationToken ct = default)
        => await Queryable(r => r.RoleId == roleId).Select(r => r.UserId).ToListAsync(ct);
}
