#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// In-memory <see cref="INotificationPreferenceRepository"/>. Only the members exercised by the
/// fan-out and settings flows are implemented.
/// </summary>
internal sealed class FakeNotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly List<NotificationPreference> _items;

    public FakeNotificationPreferenceRepository(params NotificationPreference[] seed) => _items = [.. seed];

    public List<NotificationPreference> Added { get; } = [];
    public List<NotificationPreference> Updated { get; } = [];

    public Task<NotificationPreference> FindAsync(Guid userId, string category, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(p => p.UserId == userId && p.Category == category));

    public Task<IReadOnlyList<NotificationPreference>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<NotificationPreference>>(_items.Where(p => p.UserId == userId).ToList());

    public Task<NotificationPreference> AddAsync(NotificationPreference entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<NotificationPreference> UpdateAsync(NotificationPreference entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<NotificationPreference> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(p => p.Id == key));

    // --- Unused IStandardRepository surface ---
    public IQueryable<NotificationPreference> Queryable(Expression<Func<NotificationPreference, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<NotificationPreference>> GetAsync(Expression<Func<NotificationPreference, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<NotificationPreference>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<NotificationPreference> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<NotificationPreference> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<NotificationPreference> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<NotificationPreference> RemoveAsync(NotificationPreference entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<NotificationPreference> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<NotificationPreference, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<NotificationPreference, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<NotificationPreference, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<NotificationPreference> FirstOrDefaultAsync(Expression<Func<NotificationPreference, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<NotificationPreference>> GetPagedAsync(int skip, int take, Expression<Func<NotificationPreference, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<NotificationPreference>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
