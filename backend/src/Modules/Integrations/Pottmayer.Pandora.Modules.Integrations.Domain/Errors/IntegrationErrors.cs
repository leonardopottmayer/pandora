using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Errors;

public static class IntegrationErrors
{
    public static Error UnknownProvider(string provider) =>
        Error.Validation("Integrations.UnknownProvider", $"Provider '{provider}' is not supported.");

    /// <summary>The account does not exist or is someone else's — deliberately indistinguishable.</summary>
    public static Error NotConnected =>
        Error.NotFound("Integrations.NotConnected", "No connected account with this id for this user.");

    public static Error AccountRevoked =>
        Error.Validation("Integrations.AccountRevoked", "This connection was revoked. Reconnect it to continue.");

    public static Error NoRefreshToken =>
        Error.Validation("Integrations.NoRefreshToken", "This account has no refresh token and its access has expired.");

    public static Error RefreshFailed =>
        Error.Validation("Integrations.RefreshFailed", "Could not refresh the access token. Try again shortly.");

    public static Error NotAnOAuthAccount =>
        Error.Validation("Integrations.NotAnOAuthAccount", "This account does not authenticate with OAuth.");

    public static Error NotAnApiKey =>
        Error.Validation("Integrations.NotAnApiKey", "This account is not an API key.");

    public static Error NotAPassword =>
        Error.Validation("Integrations.NotAPassword", "This account is not a username and password.");

    public static Error ApiKeyRequired =>
        Error.Validation("Integrations.ApiKeyRequired", "An API key is required.");

    public static Error UsernameRequired =>
        Error.Validation("Integrations.UsernameRequired", "A username is required.");

    public static Error PasswordRequired =>
        Error.Validation("Integrations.PasswordRequired", "A password is required.");

    public static Error LabelRequired =>
        Error.Validation("Integrations.LabelRequired", "Give the account a name.");

    public static Error SecretNotReplaceable =>
        Error.Validation("Integrations.SecretNotReplaceable", "An OAuth account is renewed by reconnecting it.");

    public static Error AccountProviderMismatch =>
        Error.Validation("Integrations.AccountProviderMismatch", "That account belongs to a different provider.");

    public static Error StateInvalid =>
        Error.Validation("Integrations.StateInvalid", "This authorization link is invalid or has expired. Start again from settings.");

    public static Error AccountNotFound =>
        Error.NotFound("Integrations.AccountNotFound", "Connected account not found.");

    public static Error InvalidRedirect =>
        Error.Validation("Integrations.InvalidRedirect", "The redirect target must be a relative path.");
}
