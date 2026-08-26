using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Errors;

public static class IntegrationErrors
{
    public static Error UnknownProvider(string provider) =>
        Error.Validation("Integrations.UnknownProvider", $"Provider '{provider}' is not supported.");

    public static Error NotConnected(string provider) =>
        Error.NotFound("Integrations.NotConnected", $"No connected '{provider}' account for this user.");

    public static Error AccountRevoked =>
        Error.Validation("Integrations.AccountRevoked", "This connection was revoked. Reconnect it to continue.");

    public static Error NoRefreshToken =>
        Error.Validation("Integrations.NoRefreshToken", "This account has no refresh token and its access has expired.");

    public static Error RefreshFailed =>
        Error.Validation("Integrations.RefreshFailed", "Could not refresh the access token. Try again shortly.");

    public static Error NotAnApiKey(string provider) =>
        Error.Validation("Integrations.NotAnApiKey", $"The connected '{provider}' account is not an API key.");

    public static Error StateInvalid =>
        Error.Validation("Integrations.StateInvalid", "This authorization link is invalid or has expired. Start again from settings.");

    public static Error AccountNotFound =>
        Error.NotFound("Integrations.AccountNotFound", "Connected account not found.");

    public static Error InvalidRedirect =>
        Error.Validation("Integrations.InvalidRedirect", "The redirect target must be a relative path.");
}
