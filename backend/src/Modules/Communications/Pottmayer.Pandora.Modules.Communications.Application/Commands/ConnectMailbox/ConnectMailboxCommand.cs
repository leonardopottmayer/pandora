using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ConnectMailbox;

/// <param name="Address">Null takes the account's: the Google email, or the IMAP username.</param>
/// <param name="ImapHost">The server fields go together; all null takes the provider's preset (Titan).</param>
public sealed record ConnectMailboxInput(
    Guid UserId,
    string? Provider,
    Guid ExternalAccountId,
    string? Address,
    string? DisplayName,
    string? ImapHost,
    int? ImapPort,
    string? SmtpHost,
    int? SmtpPort);

/// <summary>
/// Creates a mailbox on one of the caller's Integrations accounts once the provider accepted it. The
/// backfill starts on the next sync cycle.
/// </summary>
[RequiresPermission(CommunicationsPermissions.Mailboxes.Create)]
public sealed class ConnectMailboxCommand(ConnectMailboxInput input)
    : CommandBase<ConnectMailboxInput, MailboxDto>(input);
