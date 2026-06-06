#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IActivationTokenRepository"/>. Only the members exercised by the
/// sign-up / activation flows are implemented.
/// </summary>
internal sealed class FakeActivationTokenRepository : IActivationTokenRepository
{
    private readonly List<AccountActivationToken> _tokens;

    public FakeActivationTokenRepository(params AccountActivationToken[] seed) => _tokens = [.. seed];

    public IReadOnlyList<AccountActivationToken> Tokens => _tokens;

    public Task<AccountActivationToken> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<AccountActivationToken> AddAsync(AccountActivationToken entity, CancellationToken ct = default)
    {
        _tokens.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<AccountActivationToken> UpdateAsync(AccountActivationToken entity, CancellationToken ct = default)
        => Task.FromResult(entity);

    // --- Unused IStandardRepository surface ---
    public IQueryable<AccountActivationToken> Queryable(Expression<Func<AccountActivationToken, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<AccountActivationToken>> GetAsync(Expression<Func<AccountActivationToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<AccountActivationToken>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AccountActivationToken> GetByIdAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<AccountActivationToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<AccountActivationToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AccountActivationToken> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AccountActivationToken> RemoveAsync(AccountActivationToken entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<AccountActivationToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<AccountActivationToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<AccountActivationToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<AccountActivationToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AccountActivationToken> FirstOrDefaultAsync(Expression<Func<AccountActivationToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<AccountActivationToken>> GetPagedAsync(int page, int pageSize, Expression<Func<AccountActivationToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<AccountActivationToken>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
