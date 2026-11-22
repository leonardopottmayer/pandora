using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.Repositories;

public sealed class UserNotificationSettingRepository(IDataContextAccessor accessor)
    : StandardRepository<UserNotificationSetting, Guid>(accessor), IUserNotificationSettingRepository
{
    public Task<UserNotificationSetting?> FindByUserAsync(Guid userId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(s => s.UserId == userId, ct);
}
