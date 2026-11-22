using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Errors;

public static class AdminErrors
{
    public static Error MfaRequired =>
        Error.Forbidden("Admin.MfaRequired", "Turn on two-step verification in your security settings to use the admin panel.");

    public static Error RoleNotFound =>
        Error.NotFound("Admin.RoleNotFound", "Role not found.");

    public static Error RoleNameRequired =>
        Error.Validation("Admin.RoleNameRequired", "Enter the role's name.");

    public static Error RoleNameTaken =>
        Error.Conflict("Admin.RoleNameTaken", "Another role already has this name.");

    public static Error SystemRole =>
        Error.Business("Admin.SystemRole", "The admin role cannot be changed or deleted.");

    public static Error DefaultRole =>
        Error.Business("Admin.DefaultRole", "The default role cannot be deleted.");

    public static Error InvalidPattern(string pattern) =>
        Error.Validation("Admin.InvalidPattern", $"'{pattern}' matches no permission.");

    public static Error InvalidEffect =>
        Error.Validation("Admin.InvalidEffect", "An exception is either allow or deny.");

    public static Error UnknownFeature =>
        Error.NotFound("Admin.UnknownFeature", "Feature not found.");

    public static Error UserNotFound =>
        Error.NotFound("Admin.UserNotFound", "User not found.");

    public static Error LastAdmin =>
        Error.Conflict("Admin.LastAdmin", "At least one active account must keep the admin role.");
}
