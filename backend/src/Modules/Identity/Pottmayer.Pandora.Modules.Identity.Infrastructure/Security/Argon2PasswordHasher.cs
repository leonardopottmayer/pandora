using Konscious.Security.Cryptography;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using System.Security.Cryptography;
using System.Text;

namespace Pottmayer.Pandora.Modules.Identity.Infrastructure.Security;

internal sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize            = 16;
    private const int HashSize            = 32;
    private const int DegreeOfParallelism = 2;
    private const int MemorySize          = 65536; // 64 MB
    private const int Iterations          = 3;

    public string Hash(string plainText)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Compute(plainText, salt);

        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}" +
               $"${Convert.ToBase64String(salt)}" +
               $"${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string plainText, string encodedHash)
    {
        // Format: $argon2id$v=19$m=...,t=...,p=...$<salt>$<hash>
        var parts = encodedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return false;

        try
        {
            var salt         = Convert.FromBase64String(parts[3]);
            var expectedHash = Convert.FromBase64String(parts[4]);
            var actualHash   = Compute(plainText, salt);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Compute(string plainText, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plainText))
        {
            Salt                = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize          = MemorySize,
            Iterations          = Iterations
        };
        return argon2.GetBytes(HashSize);
    }
}
