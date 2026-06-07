using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;

public interface IMfaRecoveryCodeRepository : IStandardRepository<MfaRecoveryCode, Guid>
{
    Task<IReadOnlyList<MfaRecoveryCode>> ListByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task RemoveAllForUserAsync(Guid userId, CancellationToken ct = default);
}
