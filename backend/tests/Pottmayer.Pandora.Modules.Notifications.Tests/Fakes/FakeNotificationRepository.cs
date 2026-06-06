#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Notifications.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Notifications.Tests.Fakes;

/// <summary>
/// In-memory <see cref="INotificationRepository"/>. Only the members exercised by the
/// enqueue / dispatch flows are implemented.
/// </summary>
internal sealed class FakeNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _items;

    public FakeNotificationRepository(params Notification[] seed) => _items = [.. seed];

    public IReadOnlyList<Notification> Items => _items;
    public List<Notification> Added { get; } = [];
    public List<Notification> Updated { get; } = [];

    public Task<bool> ExistsByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default)
        => Task.FromResult(_items.Any(n => n.CorrelationId == correlationId));

    public Task<IReadOnlyList<Notification>> GetDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Notification>>(
            _items.Where(n => n.IsDue(now)).OrderBy(n => n.CreatedAt).Take(batchSize).ToList());

    public Task<Notification> AddAsync(Notification entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<Notification> UpdateAsync(Notification entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<Notification> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(n => n.Id == key));

    // --- Unused IStandardRepository surface ---
    public IQueryable<Notification> Queryable(Expression<Func<Notification, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<Notification>> GetAsync(Expression<Func<Notification, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Notification>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<Notification> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<Notification> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Notification> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Notification> RemoveAsync(Notification entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<Notification> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<Notification, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<Notification, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<Notification, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Notification> FirstOrDefaultAsync(Expression<Func<Notification, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Notification>> GetPagedAsync(int skip, int take, Expression<Func<Notification, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<Notification>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
