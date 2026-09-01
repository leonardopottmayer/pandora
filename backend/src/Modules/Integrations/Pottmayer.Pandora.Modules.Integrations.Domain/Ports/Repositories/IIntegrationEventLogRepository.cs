using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;

public interface IIntegrationEventLogRepository : IStandardRepository<IntegrationEventLogEntry, Guid>
{
    /// <summary>The user's most recent log entries, newest first, for the connection-health view.</summary>
    Task<IReadOnlyList<IntegrationEventLogEntry>> GetRecentByUserAsync(
        Guid userId, int limit, CancellationToken ct = default);
}
