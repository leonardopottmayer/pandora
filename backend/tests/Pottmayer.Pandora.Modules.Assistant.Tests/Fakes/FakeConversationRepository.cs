#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>In-memory <see cref="IConversationRepository"/>. Only the members the handlers use are implemented.</summary>
internal sealed class FakeConversationRepository : IConversationRepository
{
    private readonly List<Conversation> _items;

    public FakeConversationRepository(params Conversation[] seed) => _items = [.. seed];

    public List<Conversation> Added { get; } = [];
    public List<Conversation> Updated { get; } = [];

    public Task<Conversation> FindMostRecentByUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(_items.Where(c => c.UserId == userId).OrderByDescending(c => c.LastActivityAt).FirstOrDefault());

    public Task<Conversation> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(c => c.Id == key));

    public Task<Conversation> AddAsync(Conversation entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<Conversation> UpdateAsync(Conversation entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    // --- Unused IStandardRepository surface ---
    public IQueryable<Conversation> Queryable(Expression<Func<Conversation, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<Conversation>> GetAsync(Expression<Func<Conversation, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<Conversation> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<Conversation> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Conversation> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Conversation> RemoveAsync(Conversation entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<Conversation> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<Conversation, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<Conversation, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<Conversation, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Conversation> FirstOrDefaultAsync(Expression<Func<Conversation, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Conversation>> GetPagedAsync(int skip, int take, Expression<Func<Conversation, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<Conversation>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
