using System.Security.Cryptography;
using System.Text;

namespace Pottmayer.Pandora.Modules.Identity.Application.Security;

/// <summary>
/// Generates one-time recovery codes and derives the hash that is persisted. The plaintext is shown
/// to the user only once (at generation); the database keeps just the SHA-256 of the normalized code.
/// </summary>
internal static class RecoveryCodes
{
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789"; // no ambiguous chars (0/o, 1/l/i)
    private const int GroupLength = 4;
    private const int Groups = 2; // e.g. "a3kd-9mqp"

    public static IReadOnlyList<string> Generate(int count)
    {
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
            codes.Add(GenerateOne());
        return codes;
    }

    /// <summary>SHA-256 (hex, 64 chars) of the normalized code, so it can be looked up deterministically.</summary>
    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)))).ToLowerInvariant();

    private static string GenerateOne()
    {
        var sb = new StringBuilder(Groups * GroupLength + (Groups - 1));
        for (var g = 0; g < Groups; g++)
        {
            if (g > 0) sb.Append('-');
            for (var c = 0; c < GroupLength; c++)
                sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }
        return sb.ToString();
    }

    /// <summary>Strips separators/whitespace and lowercases so display formatting never affects matching.</summary>
    private static string Normalize(string code) =>
        new(code.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
