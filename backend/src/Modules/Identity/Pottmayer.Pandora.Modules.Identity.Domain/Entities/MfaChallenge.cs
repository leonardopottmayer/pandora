namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

/// <summary>
/// Short-lived ticket issued when a sign-in passes the password step but the account has MFA on.
/// The client redeems it together with a TOTP or recovery code to obtain tokens. Only the SHA-256
/// hash of the opaque ticket is persisted.
/// </summary>
public sealed class MfaChallenge
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private MfaChallenge() { }

    public static MfaChallenge Issue(Guid userId, string tokenHash, DateTimeOffset expiresAt)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        };

    public bool IsConsumable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    public void Consume(DateTimeOffset now) => ConsumedAt = now;
}
