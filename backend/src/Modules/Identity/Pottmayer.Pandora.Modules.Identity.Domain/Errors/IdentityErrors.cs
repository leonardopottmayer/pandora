using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Errors;

public static class IdentityErrors
{
    public static Error InvalidCredentials =>
        Error.Unauthorized("Identity.InvalidCredentials", "The provided credentials are invalid.");

    public static Error AccountNotActive =>
        Error.Unauthorized("Identity.AccountNotActive", "The account is not active.");

    public static Error InvalidActivationToken =>
        Error.Validation("Identity.InvalidActivationToken", "The activation token is invalid or has expired.");

    public static Error PasswordRequired =>
        Error.Validation("Identity.PasswordRequired", "Password is required.");

    public static Error WeakPassword =>
        Error.Validation("Identity.WeakPassword", "The password does not meet the required policy.");

    public static Error InvalidPasswordResetToken =>
        Error.Validation("Identity.InvalidPasswordResetToken", "The password reset token is invalid or has expired.");

    public static Error InvalidRefreshToken =>
        Error.Unauthorized("Identity.InvalidRefreshToken", "The refresh token is invalid or expired.");

    public static Error TokenReuseDetected =>
        Error.Unauthorized("Identity.TokenReuseDetected", "Refresh token reuse detected. All sessions have been revoked.");
}
