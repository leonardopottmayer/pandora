using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;
using Pottmayer.Tars.Security.Identity.Abstractions.Stores;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;

public interface IRefreshTokenRepository : IStandardRepository<StoredRefreshToken, Guid>
{
    ValueTask StoreAsync(
        string tokenId,
        string tokenHash,
        string subject,
        IReadOnlyList<ClaimData> claims,
        DateTimeOffset expiresAt,
        IReadOnlyDictionary<string, object?>? metadata,
        CancellationToken ct = default);

    ValueTask<RefreshTokenPayload?> GetAndRemoveAsync(string tokenId, string tokenHash, CancellationToken ct = default);

    ValueTask<RefreshTokenPayload?> GetAsync(string tokenId, string tokenHash, CancellationToken ct = default);

    ValueTask RevokeAsync(string tokenId, CancellationToken ct = default);

    ValueTask RevokeAllForSubjectAsync(string subject, CancellationToken ct = default);

    ValueTask<string?> TryGetSubjectForReuseAsync(string tokenId, CancellationToken ct = default);

    ValueTask<int> PurgeOldTokensAsync(
        DateTimeOffset consumedOlderThan,
        DateTimeOffset expiredOlderThan,
        CancellationToken ct = default);
}
