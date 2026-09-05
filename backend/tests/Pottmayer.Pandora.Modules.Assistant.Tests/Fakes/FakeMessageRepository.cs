#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>In-memory <see cref="IMessageRepository"/>. Only AddAsync is exercised.</summary>
internal sealed class FakeMessageRepository : IMessageRepository
{
    private readonly List<Message> _items;

    public FakeMessageRepository(params Message[] seed) => _items = [.. seed];

    public List<Message> Added { get; } = [];

    public Task<Message> AddAsync(Message entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<Message>> GetRecentByConversationAsync(Guid conversationId, int limit, CancellationToken ct = default)
    {
        IReadOnlyList<Message> recent = _items
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .TakeLast(limit)
            .ToList();
        return Task.FromResult(recent);
    }

    // --- Unused IStandardRepository surface ---
    public IQueryable<Message> Queryable(Expression<Func<Message, bool>> predicate = null) => throw new NotImplementedException();
    public Task<Message> GetByIdAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Message>> GetAsync(Expression<Func<Message, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Message>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Message> UpdateAsync(Message entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<Message> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<Message> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Message> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Message> RemoveAsync(Message entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<Message> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<Message, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<Message, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<Message, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Message> FirstOrDefaultAsync(Expression<Func<Message, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<Message>> GetPagedAsync(int skip, int take, Expression<Func<Message, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<Message>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
