using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.Repositories;

public sealed class UserPermissionRepository(IDataContextAccessor accessor)
    : StandardRepository<UserPermission, Guid>(accessor), IUserPermissionRepository
{
    public async Task<IReadOnlyList<UserPermission>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable(p => p.UserId == userId).AsNoTracking().ToListAsync(ct);
}
