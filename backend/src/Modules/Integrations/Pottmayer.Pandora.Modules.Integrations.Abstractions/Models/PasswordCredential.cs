namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;

/// <summary>
/// A username + password handed to a consumer for one connection (an IMAP/SMTP login). Transient:
/// never persisted by the consumer, never logged.
/// </summary>
public sealed record PasswordCredential(string Username, string Password)
{
    /// <summary>Keeps the password out of logs and debugger views.</summary>
    public override string ToString() => $"PasswordCredential {{ Username = {Username}, Password = *** }}";
}
