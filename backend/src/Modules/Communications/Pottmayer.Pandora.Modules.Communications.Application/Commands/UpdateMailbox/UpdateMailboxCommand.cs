using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.UpdateMailbox;

/// <param name="ImapHost">The server fields go together; all null keeps the current servers.</param>
public sealed record UpdateMailboxInput(
    Guid UserId,
    Guid MailboxId,
    string? DisplayName,
    string? ImapHost,
    int? ImapPort,
    string? SmtpHost,
    int? SmtpPort);

/// <summary>
/// Replaces the display name; new servers are tested first. A mailbox waiting for a reconnect that passes
/// the test syncs again.
/// </summary>
public sealed class UpdateMailboxCommand(UpdateMailboxInput input)
    : CommandBase<UpdateMailboxInput, MailboxDto>(input);
