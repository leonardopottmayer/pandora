using Pottmayer.Pandora.Modules.Identity.Infrastructure.Security;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void Verifies_a_correct_password()
    {
        var hash = _hasher.Hash("s3cr3t-password");

        Assert.True(_hasher.Verify("s3cr3t-password", hash));
    }

    [Fact]
    public void Rejects_an_incorrect_password()
    {
        var hash = _hasher.Hash("s3cr3t-password");

        Assert.False(_hasher.Verify("not-the-password", hash));
    }

    [Fact]
    public void Produces_a_distinct_hash_per_call_due_to_salting()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }
}
