namespace Pottmayer.Pandora.Modules.Identity.Application.Options;

/// <summary>
/// MFA settings (bound from the <c>Pandora:Identity:Mfa</c> section).
/// </summary>
public sealed class MfaOptions
{
    public const string SectionName = "Pandora:Identity:Mfa";

    /// <summary>How long an MFA challenge ticket stays valid after a password sign-in.</summary>
    public TimeSpan ChallengeLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Number of recovery codes generated when MFA is enabled or codes are regenerated.</summary>
    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>Issuer label shown by authenticator apps (the <c>otpauth://</c> issuer).</summary>
    public string Issuer { get; set; } = "Pandora";

    /// <summary>
    /// Base64-encoded 256-bit key used to encrypt TOTP secrets at rest. Must be supplied via
    /// user-secrets or environment configuration; never commit a real key.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;
}
