using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Errors;

public static class IdentityErrors
{
    public static Error InvalidCredentials =>
        Error.Unauthorized("Identity.InvalidCredentials", "The provided credentials are invalid.");

    public static Error AccountNotActive =>
        Error.Unauthorized("Identity.AccountNotActive", "The account is not active.");

    public static Error InvalidRefreshToken =>
        Error.Unauthorized("Identity.InvalidRefreshToken", "The refresh token is invalid or expired.");

    public static Error TokenReuseDetected =>
        Error.Unauthorized("Identity.TokenReuseDetected", "Refresh token reuse detected. All sessions have been revoked.");
}
