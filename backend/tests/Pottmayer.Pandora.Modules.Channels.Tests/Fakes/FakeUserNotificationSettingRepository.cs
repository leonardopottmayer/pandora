#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IUserNotificationSettingRepository"/>. Only the members the fan-out and
/// settings flows touch are implemented.
/// </summary>
internal sealed class FakeUserNotificationSettingRepository : IUserNotificationSettingRepository
{
    private readonly List<UserNotificationSetting> _items;

    public FakeUserNotificationSettingRepository(params UserNotificationSetting[] seed) => _items = [.. seed];

    public List<UserNotificationSetting> Added { get; } = [];
    public List<UserNotificationSetting> Updated { get; } = [];

    public Task<UserNotificationSetting> FindByUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(s => s.UserId == userId));

    public Task<UserNotificationSetting> AddAsync(UserNotificationSetting entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<UserNotificationSetting> UpdateAsync(UserNotificationSetting entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<UserNotificationSetting> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(s => s.Id == key));

    // --- Unused IStandardRepository surface ---
    public IQueryable<UserNotificationSetting> Queryable(Expression<Func<UserNotificationSetting, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<UserNotificationSetting>> GetAsync(Expression<Func<UserNotificationSetting, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<UserNotificationSetting>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<UserNotificationSetting> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<UserNotificationSetting> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<UserNotificationSetting> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<UserNotificationSetting> RemoveAsync(UserNotificationSetting entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<UserNotificationSetting> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<UserNotificationSetting, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<UserNotificationSetting, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<UserNotificationSetting, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<UserNotificationSetting> FirstOrDefaultAsync(Expression<Func<UserNotificationSetting, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<UserNotificationSetting>> GetPagedAsync(int skip, int take, Expression<Func<UserNotificationSetting, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<UserNotificationSetting>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
