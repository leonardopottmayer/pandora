using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

/// <summary>Keyed by (user, pattern); the single-key lookups of the base contract are not used.</summary>
public interface IUserPermissionRepository : IStandardRepository<UserPermission, Guid>
{
    Task<IReadOnlyList<UserPermission>> ListForUserAsync(Guid userId, CancellationToken ct = default);
}
