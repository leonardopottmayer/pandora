using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>Deterministic, fast hasher for tests. Not a real KDF.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    private const string Prefix = "fake:";

    public string Hash(string plainText) => Prefix + plainText;

    public int VerifyCount { get; private set; }

    public bool Verify(string plainText, string hash)
    {
        VerifyCount++;
        return hash == Prefix + plainText;
    }
}
