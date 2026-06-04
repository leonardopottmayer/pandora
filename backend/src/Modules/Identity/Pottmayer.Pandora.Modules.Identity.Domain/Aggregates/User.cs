using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Events;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;

public sealed class User : AggregateRoot<Guid>, IAuditable
{
    public string Name { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTimeOffset? EmailConfirmedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public bool MfaEnabled { get; private set; }
    public DateTimeOffset? LastSignInAt { get; private set; }
    public DateTimeOffset? LastPasswordChangedAt { get; private set; }

    public UserPreferences? Preferences { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private User() { }

    public static User Register(
        string name,
        string username,
        Email email,
        string passwordHash,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Username = username.Trim().ToLowerInvariant(),
            Email = email,
            PasswordHash = passwordHash,
            EmailConfirmedAt = now,
            LastPasswordChangedAt = now,
            CreatedAt = now
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email));
        return user;
    }

    public bool CanAuthenticate => EmailConfirmedAt is not null && DisabledAt is null;

    public bool VerifyPassword(string plainText, IPasswordHasher hasher)
        => hasher.Verify(plainText, PasswordHash);

    public void RecordSuccessfulSignIn(TimeProvider timeProvider)
        => LastSignInAt = timeProvider.GetUtcNow();

    public void UpdatePreferences(AppTheme theme)
    {
        if (Preferences is null)
            Preferences = UserPreferences.Create(theme);
        else
            Preferences.Update(theme);
    }
}
