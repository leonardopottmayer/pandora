using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;

/// <summary>
/// One sync cycle of one mailbox: labels, then what changed since the last cycle, then one backfill
/// batch. Run by the sync job for every mailbox, and by "Sync now". Succeeds with false when the
/// mailbox does not exist or waits for the user to reconnect it.
/// </summary>
public sealed record SyncMailboxInput(Guid MailboxId);

public sealed class SyncMailboxCommand(SyncMailboxInput input)
    : CommandBase<SyncMailboxInput, bool>(input);
