#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>In-memory <see cref="IAssistantProfileRepository"/>. Only the members the handlers use are implemented.</summary>
internal sealed class FakeAssistantProfileRepository : IAssistantProfileRepository
{
    private readonly List<AssistantProfile> _items;

    public FakeAssistantProfileRepository(params AssistantProfile[] seed) => _items = [.. seed];

    public IReadOnlyList<AssistantProfile> Items => _items;
    public List<AssistantProfile> Added { get; } = [];
    public List<AssistantProfile> Updated { get; } = [];

    public Task<AssistantProfile> FindByUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(p => p.UserId == userId));

    public Task<AssistantProfile> AddAsync(AssistantProfile entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<AssistantProfile> UpdateAsync(AssistantProfile entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<AssistantProfile> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(p => p.Id == key));

    // --- Unused IStandardRepository surface ---
    public IQueryable<AssistantProfile> Queryable(Expression<Func<AssistantProfile, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<AssistantProfile>> GetAsync(Expression<Func<AssistantProfile, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<AssistantProfile>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<AssistantProfile> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<AssistantProfile> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AssistantProfile> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AssistantProfile> RemoveAsync(AssistantProfile entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<AssistantProfile> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<AssistantProfile, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<AssistantProfile, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<AssistantProfile, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AssistantProfile> FirstOrDefaultAsync(Expression<Func<AssistantProfile, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<AssistantProfile>> GetPagedAsync(int skip, int take, Expression<Func<AssistantProfile, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<AssistantProfile>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
