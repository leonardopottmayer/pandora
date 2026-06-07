using System.Security.Cryptography;
using System.Text;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa.Challenge;

/// <summary>
/// Generates opaque MFA challenge tickets and derives the hash that is persisted. The plaintext
/// ticket is returned to the client by the sign-in response; the database keeps just its SHA-256.
/// </summary>
internal static class MfaTickets
{
    public static string Generate() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string ticket) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
