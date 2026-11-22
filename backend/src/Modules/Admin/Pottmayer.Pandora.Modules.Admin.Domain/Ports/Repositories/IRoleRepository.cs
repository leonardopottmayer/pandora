using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

public interface IRoleRepository : IStandardRepository<Role, Guid>
{
    Task<Role?> FindDefaultAsync(CancellationToken ct = default);

    /// <summary>Every role by name, with its patterns and how many users hold it.</summary>
    Task<IReadOnlyList<RoleWithGrants>> ListWithGrantsAsync(CancellationToken ct = default);

    Task<bool> NameTakenAsync(string name, Guid? exceptId, CancellationToken ct = default);
}
