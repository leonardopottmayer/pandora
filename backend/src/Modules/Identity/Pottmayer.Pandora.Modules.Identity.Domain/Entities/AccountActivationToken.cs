namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

/// <summary>
/// One-time, time-boxed token that confirms a user's e-mail (account activation).
/// Only the SHA-256 hash of the opaque token is persisted; the plaintext lives solely
/// in the activation e-mail.
/// </summary>
public sealed class AccountActivationToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private AccountActivationToken() { }

    public static AccountActivationToken Issue(Guid userId, string tokenHash, DateTimeOffset expiresAt)
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
