#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>In-memory <see cref="IChannelLinkTokenRepository"/>.</summary>
internal sealed class FakeChannelLinkTokenRepository : IChannelLinkTokenRepository
{
    private readonly List<ChannelLinkToken> _items;

    public FakeChannelLinkTokenRepository(params ChannelLinkToken[] seed) => _items = [.. seed];

    public List<ChannelLinkToken> Added { get; } = [];
    public List<ChannelLinkToken> Updated { get; } = [];

    public Task<ChannelLinkToken> FindByHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<ChannelLinkToken> AddAsync(ChannelLinkToken entity, CancellationToken ct = default)
    {
        _items.Add(entity);
        Added.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<ChannelLinkToken> UpdateAsync(ChannelLinkToken entity, CancellationToken ct = default)
    {
        Updated.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<ChannelLinkToken> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(t => t.Id == key));

    // --- Unused IStandardRepository surface ---
    public IQueryable<ChannelLinkToken> Queryable(Expression<Func<ChannelLinkToken, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<ChannelLinkToken>> GetAsync(Expression<Func<ChannelLinkToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<ChannelLinkToken>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<ChannelLinkToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<ChannelLinkToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ChannelLinkToken> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ChannelLinkToken> RemoveAsync(ChannelLinkToken entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<ChannelLinkToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<ChannelLinkToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<ChannelLinkToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<ChannelLinkToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ChannelLinkToken> FirstOrDefaultAsync(Expression<Func<ChannelLinkToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<ChannelLinkToken>> GetPagedAsync(int skip, int take, Expression<Func<ChannelLinkToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<ChannelLinkToken>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
