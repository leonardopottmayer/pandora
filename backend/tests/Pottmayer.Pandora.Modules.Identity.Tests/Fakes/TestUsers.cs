using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>
/// Builders for user states. A fresh <see cref="User.Register"/> is pending activation
/// (e-mail unconfirmed); <see cref="Active"/> confirms it. Disabling has no domain method
/// yet (it arrives in a later roadmap step), so that gate field is set via reflection.
/// </summary>
internal static class TestUsers
{
    public static User Active(
        FakePasswordHasher hasher,
        string password = "correct horse battery staple",
        string username = "alice",
        string email = "alice@example.com",
        string name = "Alice")
    {
        var user = Unconfirmed(hasher, password, username, email, name);
        user.ConfirmEmail(TimeProvider.System);
        return user;
    }

    public static User Disabled(FakePasswordHasher hasher, string password)
        => SetField(Active(hasher, password), nameof(User.DisabledAt), TimeProvider.System.GetUtcNow());

    public static User Unconfirmed(
        FakePasswordHasher hasher,
        string password = "correct horse battery staple",
        string username = "alice",
        string email = "alice@example.com",
        string name = "Alice")
        => User.Register(name, username, Email.Create(email), hasher.Hash(password), TimeProvider.System);

    private static User SetField(User user, string property, object? value)
    {
        typeof(User).GetProperty(property)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(user, [value]);
        return user;
    }
}
