using System.Security.Cryptography;
using System.Text;

namespace Pottmayer.Pandora.Modules.Channels.Application.Linking;

/// <summary>
/// Generates the opaque code that travels in the deep link, and derives the hash that is persisted.
/// The plaintext exists only in the link; the database keeps just its SHA-256.
/// </summary>
/// <remarks>
/// Sixteen bytes, base64url: 22 characters. Telegram caps the <c>start</c> payload at 64 and only
/// accepts <c>A-Z a-z 0-9 _ -</c>, which base64url already satisfies.
/// </remarks>
internal static class ChannelLinkTokens
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
               .TrimEnd('=')
               .Replace('+', '-')
               .Replace('/', '_');

    /// <summary>SHA-256 of the token, hex-encoded (64 chars). Deterministic, so it can be looked up.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
