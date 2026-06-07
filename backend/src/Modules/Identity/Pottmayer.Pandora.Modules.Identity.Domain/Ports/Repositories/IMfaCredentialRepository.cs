using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;

public interface IMfaCredentialRepository : IStandardRepository<MfaCredential, Guid>
{
    Task<MfaCredential?> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task RemoveByUserIdAsync(Guid userId, CancellationToken ct = default);
}
