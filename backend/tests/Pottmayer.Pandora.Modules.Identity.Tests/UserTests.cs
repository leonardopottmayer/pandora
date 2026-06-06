using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Tests.Fakes;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

public sealed class UserTests
{
    private readonly FakePasswordHasher _hasher = new();

    [Fact]
    public void Register_yields_a_pending_user_who_cannot_authenticate()
    {
        var user = User.Register("Alice", "Alice", Email.Create("alice@example.com"), _hasher.Hash("secret"), TimeProvider.System);

        Assert.Equal("alice", user.Username); // normalized
        Assert.Null(user.EmailConfirmedAt);
        Assert.Null(user.DisabledAt);
        Assert.False(user.CanAuthenticate);
        Assert.NotNull(user.LastPasswordChangedAt);
        Assert.Null(user.LastSignInAt);
        Assert.False(user.MfaEnabled);
    }

    [Fact]
    public void ConfirmEmail_activates_a_pending_user()
    {
        var user = User.Register("Alice", "Alice", Email.Create("alice@example.com"), _hasher.Hash("secret"), TimeProvider.System);

        user.ConfirmEmail(TimeProvider.System);

        Assert.NotNull(user.EmailConfirmedAt);
        Assert.True(user.CanAuthenticate);
    }

    [Fact]
    public void ConfirmEmail_is_idempotent()
    {
        var user = TestUsers.Active(_hasher, "secret");
        var confirmedAt = user.EmailConfirmedAt;

        user.ConfirmEmail(TimeProvider.System);

        Assert.Equal(confirmedAt, user.EmailConfirmedAt);
    }

    [Fact]
    public void VerifyPassword_matches_the_stored_hash()
    {
        var user = TestUsers.Active(_hasher, "secret");

        Assert.True(user.VerifyPassword("secret", _hasher));
        Assert.False(user.VerifyPassword("wrong", _hasher));
    }

    [Fact]
    public void RecordSuccessfulSignIn_stamps_last_sign_in()
    {
        var user = TestUsers.Active(_hasher, "secret");

        user.RecordSuccessfulSignIn(TimeProvider.System);

        Assert.NotNull(user.LastSignInAt);
    }

    [Fact]
    public void Disabled_user_cannot_authenticate()
    {
        var user = TestUsers.Disabled(_hasher, "secret");

        Assert.False(user.CanAuthenticate);
    }

    [Fact]
    public void Unconfirmed_user_cannot_authenticate()
    {
        var user = TestUsers.Unconfirmed(_hasher, "secret");

        Assert.False(user.CanAuthenticate);
    }
}
