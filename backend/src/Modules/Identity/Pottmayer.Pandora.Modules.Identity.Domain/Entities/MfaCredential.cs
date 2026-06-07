namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

/// <summary>
/// A user's TOTP enrollment. The shared secret is stored encrypted (reversible), unlike the
/// one-time tokens which are hashed. <see cref="ConfirmedAt"/> is null while the setup is pending
/// (secret generated but not yet proven by a valid code).
/// </summary>
public sealed class MfaCredential
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string SecretCipher { get; private set; } = string.Empty;
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private MfaCredential() { }

    public static MfaCredential Issue(Guid userId, string secretCipher, DateTimeOffset now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SecretCipher = secretCipher,
            CreatedAt = now
        };

    public bool IsConfirmed => ConfirmedAt is not null;

    public void Confirm(DateTimeOffset now)
    {
        if (ConfirmedAt is null)
            ConfirmedAt = now;
    }
}
