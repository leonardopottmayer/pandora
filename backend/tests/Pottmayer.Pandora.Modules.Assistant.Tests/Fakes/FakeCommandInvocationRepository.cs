#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>In-memory <see cref="ICommandInvocationRepository"/>. Only the members the handlers use are implemented.</summary>
internal sealed class FakeCommandInvocationRepository : ICommandInvocationRepository
{
    private readonly List<CommandInvocation> _items = [];

    public List<CommandInvocation> Added { get; } = [];
    public List<CommandInvocation> Updated { get; } = [];

    public Task<CommandInvocation> AddAsync(CommandInvocation entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<CommandInvocation>> ListRecentByUserAsync(Guid userId, int limit, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<CommandInvocation>)_items
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .ToList());

    public Task<CommandInvocation> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(i => i.Id == key));

    public Task<CommandInvocation> UpdateAsync(CommandInvocation entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    // --- Unused IStandardRepository surface ---
    public IQueryable<CommandInvocation> Queryable(Expression<Func<CommandInvocation, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<CommandInvocation>> GetAsync(Expression<Func<CommandInvocation, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<CommandInvocation>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<CommandInvocation> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<CommandInvocation> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<CommandInvocation> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<CommandInvocation> RemoveAsync(CommandInvocation entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<CommandInvocation> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<CommandInvocation, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<CommandInvocation, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<CommandInvocation, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<CommandInvocation> FirstOrDefaultAsync(Expression<Func<CommandInvocation, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<CommandInvocation>> GetPagedAsync(int skip, int take, Expression<Func<CommandInvocation, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<CommandInvocation>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
