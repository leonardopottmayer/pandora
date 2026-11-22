using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailboxNow;

public sealed record SyncMailboxNowInput(Guid UserId, Guid MailboxId);

/// <summary>
/// "Sync now": one cycle, then the mailbox as it is. A mailbox waiting for a reconnect is tested first;
/// a refused test answers with the provider's error and changes nothing.
/// </summary>
[RequiresPermission(CommunicationsPermissions.Mailboxes.Update)]
public sealed class SyncMailboxNowCommand(SyncMailboxNowInput input)
    : CommandBase<SyncMailboxNowInput, MailboxDto>(input);
