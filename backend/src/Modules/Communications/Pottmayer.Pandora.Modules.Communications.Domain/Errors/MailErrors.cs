using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Errors;

public static class MailErrors
{
    /// <summary>The server refused the credential. The mailbox goes <c>needs-reconnect</c>.</summary>
    public static Error MailboxAuthFailed(string detail) =>
        Error.Validation("Communications.MailboxAuthFailed", $"The mail server refused the login: {detail}");

    /// <summary>The Google account was connected without the Gmail scopes. The screen asks to reconnect.</summary>
    public static Error MissingScopes =>
        Error.Validation("Communications.MissingScopes", "This Google account has not granted access to Gmail.");

    /// <summary>The provider no longer knows the sync cursor (Gmail history expired, IMAP UIDVALIDITY changed).</summary>
    public static Error SyncCursorExpired =>
        Error.Validation("Communications.SyncCursorExpired", "The mailbox must be read again from the start.");

    /// <summary>Anything else the provider answered with; the next cycle retries.</summary>
    public static Error ProviderFailed(string detail) =>
        Error.Validation("Communications.ProviderFailed", detail);

    /// <summary>The cycle threw instead of answering; the details are in the server log. The next cycle retries.</summary>
    public static Error SyncFailed =>
        Error.Validation("Communications.SyncFailed", "The sync stopped on an unexpected error. The server log has the details.");

    public static Error MailboxNotFound =>
        Error.NotFound("Communications.MailboxNotFound", "This mailbox does not exist.");

    public static Error MessageNotFound =>
        Error.NotFound("Communications.MessageNotFound", "This message does not exist.");

    public static Error AttachmentNotFound =>
        Error.NotFound("Communications.AttachmentNotFound", "This attachment does not exist.");

    /// <summary>The same answer for a missing account and someone else's.</summary>
    public static Error AccountNotFound =>
        Error.NotFound("Communications.AccountNotFound", "This connected account does not exist.");

    public static Error UnsupportedProvider(string? provider) =>
        Error.Validation("Communications.UnsupportedProvider", $"'{provider}' is not a mail provider.");

    public static Error AccountProviderMismatch(string provider) =>
        Error.Validation("Communications.AccountProviderMismatch", $"This account cannot be used as a '{provider}' mailbox.");

    public static Error AddressRequired =>
        Error.Validation("Communications.AddressRequired", "The mailbox needs an email address.");

    public static Error ServerSettingsInvalid =>
        Error.Validation("Communications.ServerSettingsInvalid", "The mailbox needs valid IMAP and SMTP hosts and ports.");

    public static Error DisplayNameTooLong =>
        Error.Validation("Communications.DisplayNameTooLong", $"Use at most {Entities.Mailbox.DisplayNameMaxLength} characters.");

    public static Error AlreadyConnected =>
        Error.Conflict("Communications.AlreadyConnected", "This account already has a mailbox.");

    public static Error InvalidCursor =>
        Error.Validation("Communications.InvalidCursor", "The page cursor is not valid.");
}
