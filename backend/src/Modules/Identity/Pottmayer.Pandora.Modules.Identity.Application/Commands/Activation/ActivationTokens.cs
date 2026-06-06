using System.Security.Cryptography;
using System.Text;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;

/// <summary>
/// Generates opaque activation tokens and derives the hash that is persisted.
/// The plaintext token travels only in the activation e-mail; the database keeps just its SHA-256.
/// </summary>
internal static class ActivationTokens
{
    public static string Generate() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>SHA-256 of the token, hex-encoded (64 chars). Deterministic, so it can be looked up.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
