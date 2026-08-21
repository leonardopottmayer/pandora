using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;

public interface INotificationPreferenceRepository : IStandardRepository<NotificationPreference, Guid>
{
    /// <summary>The user's preference for one category, or null when they never set one.</summary>
    Task<NotificationPreference?> FindAsync(Guid userId, string category, CancellationToken ct = default);

    /// <summary>Every preference the user has, for the settings screen.</summary>
    Task<IReadOnlyList<NotificationPreference>> GetByUserAsync(Guid userId, CancellationToken ct = default);
}
