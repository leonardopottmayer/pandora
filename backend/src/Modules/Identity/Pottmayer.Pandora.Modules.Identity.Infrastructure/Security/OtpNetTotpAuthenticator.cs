using OtpNet;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Identity.Infrastructure.Security;

/// <summary>
/// <see cref="ITotpAuthenticator"/> backed by Otp.NET. Uses the RFC 6238 defaults (SHA-1, 6 digits,
/// 30s step) that authenticator apps assume, and tolerates ±1 step of clock drift on verification.
/// </summary>
internal sealed class OtpNetTotpAuthenticator : ITotpAuthenticator
{
    private const int SecretBytes = 20; // 160-bit, the recommended TOTP key size
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    public string GenerateSecret()
        => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretBytes));

    public string BuildOtpauthUri(string secret, string issuer, string accountName)
        => new OtpUri(OtpType.Totp, secret, accountName, issuer).ToString();

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(code.Trim(), out _, Window);
    }
}
