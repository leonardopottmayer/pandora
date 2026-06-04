namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

public sealed class StoredRefreshToken
{
    public Guid Id { get; set; }
    public string TokenId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string ClaimsJson { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
