using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Users.Domain.Errors;

public static class UserErrors
{
    public static Error EmailAlreadyRegistered =>
        Error.Validation("Users.EmailAlreadyRegistered", "The provided email is already registered.");

    public static Error UsernameAlreadyRegistered =>
        Error.Validation("Users.UsernameAlreadyRegistered", "The provided username is already taken.");

    public static Error InvalidEmail =>
        Error.Validation("Users.InvalidEmail", "The provided email is invalid.");

    public static Error InvalidUsername =>
        Error.Validation("Users.InvalidUsername", "Username is required.");

    public static Error InvalidName =>
        Error.Validation("Users.InvalidName", "Name is required.");

    public static Error InvalidPassword =>
        Error.Validation("Users.InvalidPassword", "Password is required.");

    public static Error InvalidTheme(string theme) =>
        Error.Validation("Users.InvalidTheme", $"Theme '{theme}' is not supported.");

    public static Error NotFound =>
        Error.NotFound("Users.NotFound", "User not found.");

    public static Error PreferencesNotFound =>
        Error.NotFound("Users.PreferencesNotFound", "User preferences not found.");
}
