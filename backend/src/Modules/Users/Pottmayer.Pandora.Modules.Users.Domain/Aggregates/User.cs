using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Users.Domain.Entities;
using Pottmayer.Pandora.Modules.Users.Domain.Events;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Users.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Users.Domain.Aggregates;

public sealed class User : AggregateRoot<Guid>, IAuditable
{
    public string Name { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string Password { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; } = null!;
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
        string hashedPassword,
        TimeProvider timeProvider)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Username = username.Trim().ToLowerInvariant(),
            Email = email,
            Password = hashedPassword,
            Status = UserStatus.Blocked,
            CreatedAt = timeProvider.GetUtcNow()
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email.Value));
        return user;
    }

    public void Activate() => Status = UserStatus.Active;
    public void Block() => Status = UserStatus.Blocked;

    public bool VerifyPassword(string plainText, IPasswordHasher hasher)
        => hasher.Verify(plainText, Password);

    public bool IsActive => Status == UserStatus.Active;

    public void UpdatePreferences(AppTheme theme)
    {
        if (Preferences is null)
            Preferences = UserPreferences.Create(theme);
        else
            Preferences.Update(theme);
    }
}
