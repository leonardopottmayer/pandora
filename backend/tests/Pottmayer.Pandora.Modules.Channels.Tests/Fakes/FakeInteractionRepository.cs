#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>In-memory <see cref="IInteractionRepository"/>. Only the ingress/enqueue members are used.</summary>
internal sealed class FakeInteractionRepository : IInteractionRepository
{
    private readonly List<Interaction> _items;

    public FakeInteractionRepository(params Interaction[] seed) => _items = [.. seed];

    public List<Interaction> Added { get; } = [];
    public List<Interaction> Updated { get; } = [];

    public Task<Interaction> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(i => i.Id == key));

    public Task<Interaction> AddAsync(Interaction entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<Interaction> UpdateAsync(Interaction entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    // --- Unused IStandardRepository surface ---
    public IQueryable<Interaction> Queryable(Expression<Func<Interaction, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<Interaction>> GetAsync(Expression<Func<Interaction, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Interaction>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<Interaction> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<Interaction> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Interaction> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Interaction> RemoveAsync(Interaction entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<Interaction> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<Interaction, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<Interaction, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<Interaction, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Interaction> FirstOrDefaultAsync(Expression<Func<Interaction, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Interaction>> GetPagedAsync(int skip, int take, Expression<Func<Interaction, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<Interaction>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
