namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;

/// <summary>
/// Generates and verifies time-based one-time passwords (RFC 6238), compatible with apps such as
/// Microsoft Authenticator and Google Authenticator.
/// </summary>
public interface ITotpAuthenticator
{
    /// <summary>Creates a new Base32-encoded shared secret.</summary>
    string GenerateSecret();

    /// <summary>Builds the <c>otpauth://totp/...</c> URI consumed by authenticator apps (QR code).</summary>
    string BuildOtpauthUri(string secret, string issuer, string accountName);

    /// <summary>Validates a code against the secret, allowing a small clock-drift window.</summary>
    bool VerifyCode(string secret, string code);
}
