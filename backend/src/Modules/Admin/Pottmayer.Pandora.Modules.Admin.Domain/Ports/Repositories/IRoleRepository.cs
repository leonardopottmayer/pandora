using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

public interface IRoleRepository : IStandardRepository<Role, Guid>
{
    Task<Role?> FindDefaultAsync(CancellationToken ct = default);
}
