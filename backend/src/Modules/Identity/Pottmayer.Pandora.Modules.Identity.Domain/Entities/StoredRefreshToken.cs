namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

public sealed class StoredRefreshToken
{
    public Guid Id { get; set; }

    /// <summary>The refresh token id issued by Tars — the lookup key.</summary>
    public string TokenId { get; set; } = string.Empty;

    /// <summary>Hash of the token's secret segment, validated on consume.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The user's subject identifier (user Id as string).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>JSON-serialized list of claims included at token issuance.</summary>
    public string ClaimsJson { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Optional JSON metadata stored at issuance.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>When the token was consumed (null = still valid).</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
