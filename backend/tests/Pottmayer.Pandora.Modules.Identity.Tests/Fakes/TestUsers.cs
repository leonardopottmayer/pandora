using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>
/// Builders for user states. State transitions (disable / e-mail activation)
/// have no domain methods yet (they arrive in later roadmap steps), so the
/// gate fields are set via reflection.
/// </summary>
internal static class TestUsers
{
    public static User Active(
        FakePasswordHasher hasher,
        string password = "correct horse battery staple",
        string username = "alice",
        string email = "alice@example.com",
        string name = "Alice")
        => User.Register(name, username, Email.Create(email), hasher.Hash(password), TimeProvider.System);

    public static User Disabled(FakePasswordHasher hasher, string password)
        => SetField(Active(hasher, password), nameof(User.DisabledAt), TimeProvider.System.GetUtcNow());

    public static User Unconfirmed(FakePasswordHasher hasher, string password)
        => SetField(Active(hasher, password), nameof(User.EmailConfirmedAt), null);

    private static User SetField(User user, string property, object? value)
    {
        typeof(User).GetProperty(property)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(user, [value]);
        return user;
    }
}
