#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>In-memory <see cref="IInboundUpdateRepository"/>. Only the ingress members are implemented.</summary>
internal sealed class FakeInboundUpdateRepository : IInboundUpdateRepository
{
    private readonly List<InboundUpdate> _items;

    public FakeInboundUpdateRepository(params InboundUpdate[] seed) => _items = [.. seed];

    public List<InboundUpdate> Added { get; } = [];

    public Task<bool> ExistsAsync(string provider, long providerUpdateId, CancellationToken ct = default)
        => Task.FromResult(_items.Any(u => u.Provider == provider && u.ProviderUpdateId == providerUpdateId));

    public Task<long?> GetLastUpdateIdAsync(string provider, CancellationToken ct = default)
        => Task.FromResult(_items.Where(u => u.Provider == provider)
            .Select(u => (long?)u.ProviderUpdateId).Max());

    public Task<InboundUpdate> AddAsync(InboundUpdate entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<InboundUpdate> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(u => u.Id == key));

    // --- Unused IStandardRepository surface ---
    public IQueryable<InboundUpdate> Queryable(Expression<Func<InboundUpdate, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<InboundUpdate>> GetAsync(Expression<Func<InboundUpdate, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<InboundUpdate>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<InboundUpdate> UpdateAsync(InboundUpdate entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<InboundUpdate> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<InboundUpdate> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<InboundUpdate> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<InboundUpdate> RemoveAsync(InboundUpdate entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<InboundUpdate> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<InboundUpdate, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<InboundUpdate, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<InboundUpdate, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<InboundUpdate> FirstOrDefaultAsync(Expression<Func<InboundUpdate, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<InboundUpdate>> GetPagedAsync(int skip, int take, Expression<Func<InboundUpdate, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<InboundUpdate>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
