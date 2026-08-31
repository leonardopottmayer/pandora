using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;

public interface IUserNotificationSettingRepository : IStandardRepository<UserNotificationSetting, Guid>
{
    /// <summary>The user's settings row, or null when they never touched any of these settings.</summary>
    Task<UserNotificationSetting?> FindByUserAsync(Guid userId, CancellationToken ct = default);
}
