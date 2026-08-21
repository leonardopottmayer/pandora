using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface IAlertRepository : IStandardRepository<Alert, Guid>
{
    /// <summary>One alert scoped to its owner.</summary>
    Task<Alert?> FindAsync(Guid userId, Guid alertId, CancellationToken ct = default);

    /// <summary>Every alert on one subject, for listing and for carrying alerts to a recurring task's next instance.</summary>
    Task<IReadOnlyList<Alert>> GetBySubjectAsync(
        Guid userId, AlertSubjectType subjectType, Guid subjectId, CancellationToken ct = default);

    /// <summary>Enabled task alerts, across users — the scan root of the task-alert sweep.</summary>
    Task<IReadOnlyList<Alert>> GetEnabledTaskAlertsAsync(int batchSize, CancellationToken ct = default);
}
