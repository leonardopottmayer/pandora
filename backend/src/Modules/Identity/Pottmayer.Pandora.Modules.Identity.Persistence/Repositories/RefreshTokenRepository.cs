using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;
using Pottmayer.Tars.Security.Identity.Abstractions.Stores;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.Repositories;

public sealed class RefreshTokenRepository(IDataContextAccessor accessor)
    : StandardRepository<StoredRefreshToken, Guid>(accessor), IRefreshTokenRepository
{
    public async ValueTask StoreAsync(
        string tokenId,
        string tokenHash,
        string subject,
        IReadOnlyList<ClaimData> claims,
        DateTimeOffset expiresAt,
        IReadOnlyDictionary<string, object?>? metadata,
        CancellationToken ct = default)
    {
        var entity = new StoredRefreshToken
        {
            Id           = Guid.CreateVersion7(),
            TokenId      = tokenId,
            TokenHash    = tokenHash,
            Subject      = subject,
            ClaimsJson   = JsonSerializer.Serialize(claims),
            ExpiresAt    = expiresAt,
            MetadataJson = metadata is not null ? JsonSerializer.Serialize(metadata) : null
        };

        await AddAsync(entity, ct);
    }

    public async ValueTask<RefreshTokenPayload?> GetAndRemoveAsync(string tokenId, string tokenHash, CancellationToken ct = default)
    {
        var id     = ExtractId(tokenId);
        var entity = await Queryable()
            .FirstOrDefaultAsync(t => t.TokenId == id && t.ConsumedAt == null && t.ExpiresAt > DateTimeOffset.UtcNow, ct);

        if (entity is null || !HashesMatch(entity.TokenHash, tokenHash))
            return null;

        entity.ConsumedAt = DateTimeOffset.UtcNow;
        await UpdateAsync(entity, ct);

        return ToPayload(entity);
    }

    public async ValueTask<RefreshTokenPayload?> GetAsync(string tokenId, string tokenHash, CancellationToken ct = default)
    {
        var id     = ExtractId(tokenId);
        var entity = await Queryable()
            .FirstOrDefaultAsync(t => t.TokenId == id && t.ConsumedAt == null && t.ExpiresAt > DateTimeOffset.UtcNow, ct);

        return entity is null || !HashesMatch(entity.TokenHash, tokenHash) ? null : ToPayload(entity);
    }

    public async ValueTask RevokeAsync(string tokenId, CancellationToken ct = default)
    {
        var id     = ExtractId(tokenId);
        var entity = await Queryable().FirstOrDefaultAsync(t => t.TokenId == id && t.ConsumedAt == null, ct);
        if (entity is null) return;

        entity.ConsumedAt = DateTimeOffset.UtcNow;
        await UpdateAsync(entity, ct);
    }

    public async ValueTask RevokeAllForSubjectAsync(string subject, CancellationToken ct = default)
    {
        var tokens = await Queryable()
            .Where(t => t.Subject == subject && t.ConsumedAt == null)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
            token.ConsumedAt = now;

        await UpdateRangeAsync(tokens, ct);
    }

    public async ValueTask<string?> TryGetSubjectForReuseAsync(string tokenId, CancellationToken ct = default)
    {
        var id     = ExtractId(tokenId);
        var entity = await Queryable()
            .FirstOrDefaultAsync(t => t.TokenId == id && t.ConsumedAt != null, ct);

        return entity?.Subject;
    }

    public async ValueTask<int> PurgeOldTokensAsync(
        DateTimeOffset consumedOlderThan,
        DateTimeOffset expiredOlderThan,
        CancellationToken ct = default)
    {
        var toDelete = await Queryable()
            .Where(t => (t.ConsumedAt != null && t.ConsumedAt < consumedOlderThan)
                     || (t.ConsumedAt == null && t.ExpiresAt < expiredOlderThan))
            .ToListAsync(ct);

        await RemoveRangeAsync(toDelete, ct);
        return toDelete.Count;
    }

    private static string ExtractId(string opaqueToken)
    {
        var idx = opaqueToken.IndexOf(':');
        return idx >= 0 ? opaqueToken[..idx] : opaqueToken;
    }

    private static bool HashesMatch(string stored, string provided) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(provided));

    private static RefreshTokenPayload ToPayload(StoredRefreshToken entity)
    {
        var claims = JsonSerializer.Deserialize<List<ClaimData>>(entity.ClaimsJson) ?? [];
        return new RefreshTokenPayload
        {
            Subject  = entity.Subject,
            Claims   = claims,
            Metadata = entity.MetadataJson is not null
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.MetadataJson)
                : null
        };
    }
}
