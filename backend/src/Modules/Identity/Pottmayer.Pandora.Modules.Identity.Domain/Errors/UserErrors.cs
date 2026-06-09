using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Errors;

public static class UserErrors
{
    public static Error EmailOrUsernameAlreadyRegistered =>
        Error.Validation("Users.EmailOrUsernameAlreadyRegistered", "The provided email or username is already registered.");

    public static Error InvalidEmail =>
        Error.Validation("Users.InvalidEmail", "The provided email is invalid.");

    public static Error InvalidUsername =>
        Error.Validation("Users.InvalidUsername", "Username is required.");

    public static Error InvalidName =>
        Error.Validation("Users.InvalidName", "Name is required.");

    public static Error InvalidTheme(string theme) =>
        Error.Validation("Users.InvalidTheme", $"Theme '{theme}' is not supported.");

    public static Error InvalidLanguage(string language) =>
        Error.Validation("Users.InvalidLanguage", $"Language '{language}' is not supported.");

    public static Error NotFound =>
        Error.NotFound("Users.NotFound", "User not found.");

    public static Error PreferencesNotFound =>
        Error.NotFound("Users.PreferencesNotFound", "User preferences not found.");
}
