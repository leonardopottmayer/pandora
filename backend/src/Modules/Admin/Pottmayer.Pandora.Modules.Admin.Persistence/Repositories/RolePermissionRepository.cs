using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.Repositories;

public sealed class RolePermissionRepository(IDataContextAccessor accessor)
    : StandardRepository<RolePermission, Guid>(accessor), IRolePermissionRepository
{
    public async Task<IReadOnlyList<RolePermission>> ListForRoleAsync(Guid roleId, CancellationToken ct = default)
        => await Queryable(p => p.RoleId == roleId).ToListAsync(ct);
}
