using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.Repositories;

public sealed class NotificationPreferenceRepository(IDataContextAccessor accessor)
    : StandardRepository<NotificationPreference, Guid>(accessor), INotificationPreferenceRepository
{
    public Task<NotificationPreference?> FindAsync(Guid userId, string category, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(p => p.UserId == userId && p.Category == category, ct);

    public async Task<IReadOnlyList<NotificationPreference>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Queryable()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Category)
            .ToListAsync(ct);
}
