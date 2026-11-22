using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

public interface IAuditEntryRepository : IStandardRepository<AuditEntry, Guid>
{
    /// <summary>Newest first, older than <paramref name="before"/> when given.</summary>
    Task<IReadOnlyList<AuditEntry>> ListAsync(
        Guid? actorId, string? targetType, string? targetId, DateTimeOffset? before, int limit,
        CancellationToken ct = default);
}
