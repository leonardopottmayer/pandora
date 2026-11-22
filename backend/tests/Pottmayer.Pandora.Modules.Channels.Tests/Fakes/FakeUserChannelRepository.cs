#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IUserChannelRepository"/>. Only the members exercised by the dispatch and
/// linking flows are implemented.
/// </summary>
internal sealed class FakeUserChannelRepository : IUserChannelRepository
{
    private readonly List<UserChannel> _items;

    public FakeUserChannelRepository(params UserChannel[] seed) => _items = [.. seed];

    public IReadOnlyList<UserChannel> Items => _items;
    public List<UserChannel> Added { get; } = [];
    public List<UserChannel> Updated { get; } = [];

    public Task<UserChannel> FindAsync(Guid userId, Channel channel, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(c => c.UserId == userId && c.Channel == channel));

    public Task<IReadOnlyList<UserChannel>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UserChannel>>(_items.Where(c => c.UserId == userId).ToList());

    public Task<UserChannel> FindByAddressAsync(Channel channel, NotificationAddress address, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(c => c.Channel == channel && c.Address == address));

    public Task<UserChannel> AddAsync(UserChannel entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<UserChannel> UpdateAsync(UserChannel entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<UserChannel> RemoveAsync(UserChannel entity, CancellationToken ct = default)
    {
        _items.Remove(entity);
        return Task.FromResult(entity);
    }

    public Task<UserChannel> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(c => c.Id == key));

    // --- Unused IStandardRepository surface ---
    public IQueryable<UserChannel> Queryable(Expression<Func<UserChannel, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<UserChannel>> GetAsync(Expression<Func<UserChannel, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<UserChannel>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<UserChannel> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<UserChannel> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<UserChannel> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<UserChannel> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<UserChannel, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<UserChannel, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<UserChannel, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<UserChannel> FirstOrDefaultAsync(Expression<Func<UserChannel, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<UserChannel>> GetPagedAsync(int skip, int take, Expression<Func<UserChannel, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<UserChannel>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
