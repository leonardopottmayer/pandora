namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

/// <summary>
/// One-time recovery code used as a fallback second factor. Only the SHA-256 hash is persisted;
/// the plaintext is shown to the user a single time when the codes are generated.
/// </summary>
public sealed class MfaRecoveryCode
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private MfaRecoveryCode() { }

    public static MfaRecoveryCode Issue(Guid userId, string codeHash, DateTimeOffset now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CodeHash = codeHash,
            CreatedAt = now
        };

    public bool IsConsumable => ConsumedAt is null;

    public void Consume(DateTimeOffset now) => ConsumedAt = now;
}
