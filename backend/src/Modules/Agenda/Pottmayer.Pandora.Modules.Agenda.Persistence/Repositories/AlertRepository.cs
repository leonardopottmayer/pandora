using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class AlertRepository(IDataContextAccessor accessor)
    : StandardRepository<Alert, Guid>(accessor), IAlertRepository
{
    public Task<Alert?> FindAsync(Guid userId, Guid alertId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(a => a.UserId == userId && a.Id == alertId, ct);

    public async Task<IReadOnlyList<Alert>> GetBySubjectAsync(
        Guid userId, AlertSubjectType subjectType, Guid subjectId, CancellationToken ct = default) =>
        await Queryable()
            .Where(a => a.UserId == userId && a.SubjectType == subjectType && a.SubjectId == subjectId)
            .OrderBy(a => a.OffsetMinutes)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Alert>> GetEnabledTaskAlertsAsync(int batchSize, CancellationToken ct = default) =>
        await Queryable()
            .Where(a => a.IsEnabled && a.SubjectType == AlertSubjectType.Task)
            .OrderBy(a => a.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
}
