#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.Query;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;
using Pottmayer.Tars.Security.Identity.Abstractions.Stores;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>
/// Fake <see cref="IRefreshTokenRepository"/>. Only reuse-detection is wired;
/// token issue/consume go through <see cref="FakeRefreshTokenService"/>.
/// </summary>
internal sealed class FakeRefreshTokenRepository(string reuseSubject = null) : IRefreshTokenRepository
{
    /// <summary>Subjects passed to <see cref="RevokeAllForSubjectAsync"/>, in call order.</summary>
    public List<string> RevokedSubjects { get; } = [];

    public ValueTask<string> TryGetSubjectForReuseAsync(string tokenId, CancellationToken ct = default)
        => ValueTask.FromResult(reuseSubject);

    public ValueTask RevokeAllForSubjectAsync(string subject, CancellationToken ct = default)
    {
        RevokedSubjects.Add(subject);
        return ValueTask.CompletedTask;
    }

    public ValueTask StoreAsync(string tokenId, string tokenHash, string subject, IReadOnlyList<ClaimData> claims, DateTimeOffset expiresAt, IReadOnlyDictionary<string, object> metadata, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<RefreshTokenPayload> GetAndRemoveAsync(string tokenId, string tokenHash, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<RefreshTokenPayload> GetAsync(string tokenId, string tokenHash, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask RevokeAsync(string tokenId, CancellationToken ct = default) => throw new NotImplementedException();
    public ValueTask<int> PurgeOldTokensAsync(DateTimeOffset consumedOlderThan, DateTimeOffset expiredOlderThan, CancellationToken ct = default) => throw new NotImplementedException();

    // --- Unused IStandardRepository surface ---
    public IQueryable<StoredRefreshToken> Queryable(Expression<Func<StoredRefreshToken, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<StoredRefreshToken>> GetAsync(Expression<Func<StoredRefreshToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<StoredRefreshToken>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<StoredRefreshToken> GetByIdAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<StoredRefreshToken> AddAsync(StoredRefreshToken entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<StoredRefreshToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<StoredRefreshToken> UpdateAsync(StoredRefreshToken entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<StoredRefreshToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<StoredRefreshToken> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<StoredRefreshToken> RemoveAsync(StoredRefreshToken entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<StoredRefreshToken> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<StoredRefreshToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<StoredRefreshToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<StoredRefreshToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<StoredRefreshToken> FirstOrDefaultAsync(Expression<Func<StoredRefreshToken, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<StoredRefreshToken>> GetPagedAsync(int page, int pageSize, Expression<Func<StoredRefreshToken, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<StoredRefreshToken>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
