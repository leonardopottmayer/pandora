using System.Security.Cryptography;
using System.Text;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Oauth;

/// <summary>
/// Generates the values that secure the authorization-code flow: the CSRF <c>state</c> and the PKCE
/// verifier/challenge pair (S256). All base64url, no padding.
/// </summary>
internal static class PkceCodes
{
    /// <summary>A random CSRF token, 32 bytes → 43 base64url chars.</summary>
    public static string NewState() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>A random PKCE code verifier, 32 bytes → 43 base64url chars (within the 43–128 spec range).</summary>
    public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The S256 challenge for a verifier: base64url(SHA-256(verifier)).</summary>
    public static string Challenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
