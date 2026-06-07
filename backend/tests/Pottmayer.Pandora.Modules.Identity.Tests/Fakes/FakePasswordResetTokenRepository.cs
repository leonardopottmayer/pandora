#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IPasswordResetTokenRepository"/>. Only the members exercised by the
/// request / reset flows are implemented.
/// </summary>
internal sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly List<PasswordResetToken> _tokens;

    public FakePasswordResetTokenRepository(params PasswordResetToken[] seed) => _tokens = [.. seed];

    public IReadOnlyList<PasswordResetToken> Tokens => _tokens;

    public Task<PasswordResetToken> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<PasswordResetToken> AddAsync(PasswordResetToken entity, CancellationToken ct = default)
    {
        _tokens.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<PasswordResetToken> UpdateAsync(PasswordResetToken entity, CancellationToken ct = default)
        => Task.FromResult(entity);

    // --- Unused IStandardRepository surface ---
    public IQueryable<PasswordResetToken> Queryable(Expression<Func<PasswordResetToken, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<PasswordResetToken>> GetAsync(Expression<Func<PasswordResetToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<PasswordResetToken>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<PasswordResetToken> GetByIdAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<PasswordResetToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<PasswordResetToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<PasswordResetToken> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<PasswordResetToken> RemoveAsync(PasswordResetToken entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<PasswordResetToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<PasswordResetToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<PasswordResetToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<PasswordResetToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<PasswordResetToken> FirstOrDefaultAsync(Expression<Func<PasswordResetToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<PasswordResetToken>> GetPagedAsync(int page, int pageSize, Expression<Func<PasswordResetToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<PasswordResetToken>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
