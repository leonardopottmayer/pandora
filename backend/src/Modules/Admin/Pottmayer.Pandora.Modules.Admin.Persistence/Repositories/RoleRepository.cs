using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.Repositories;

public sealed class RoleRepository(IDataContextAccessor accessor)
    : StandardRepository<Role, Guid>(accessor), IRoleRepository
{
    public Task<Role?> FindDefaultAsync(CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(r => r.IsDefault, ct);
}
