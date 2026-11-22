using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

/// <summary>Keyed by (role, pattern); the single-key lookups of the base contract are not used.</summary>
public interface IRolePermissionRepository : IStandardRepository<RolePermission, Guid>
{
    Task<IReadOnlyList<RolePermission>> ListForRoleAsync(Guid roleId, CancellationToken ct = default);
}
