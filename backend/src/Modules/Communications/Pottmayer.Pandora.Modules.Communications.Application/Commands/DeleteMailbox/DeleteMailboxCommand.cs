using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.DeleteMailbox;

public sealed record DeleteMailboxInput(Guid UserId, Guid MailboxId);

/// <summary>Deletes the local copy (labels, messages, cached bodies). The credential stays in Integrations.</summary>
[RequiresPermission(CommunicationsPermissions.Mailboxes.Delete)]
public sealed class DeleteMailboxCommand(DeleteMailboxInput input)
    : CommandBase<DeleteMailboxInput, bool>(input);
