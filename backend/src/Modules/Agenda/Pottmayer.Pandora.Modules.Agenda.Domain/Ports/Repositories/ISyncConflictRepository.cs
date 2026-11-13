using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface ISyncConflictRepository : IStandardRepository<SyncConflict, Guid>
{
    /// <summary>The user's most recent conflicts, newest first.</summary>
    Task<IReadOnlyList<SyncConflict>> GetRecentAsync(Guid userId, int limit, CancellationToken ct = default);
}
