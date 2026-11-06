using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

/// <summary>Keyed by (user, role); the single-key lookups of the base contract are not used.</summary>
public interface IUserRoleRepository : IStandardRepository<UserRole, Guid>
{
    Task<bool> ExistsAsync(Guid userId, Guid roleId, CancellationToken ct = default);

    /// <summary>Every pattern held by the roles of the user.</summary>
    Task<IReadOnlyList<string>> ListPatternsAsync(Guid userId, CancellationToken ct = default);
}
