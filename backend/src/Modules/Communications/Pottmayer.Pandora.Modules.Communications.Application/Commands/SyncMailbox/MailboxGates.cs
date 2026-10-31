using System.Collections.Concurrent;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;

/// <summary>
/// The in-process gates of each mailbox (the same single-instance assumption as
/// <c>ExternalCredentialProvider</c>). <see cref="Cycle"/> keeps the job and "Sync now" from running two cycles at
/// once. <see cref="Action"/> serializes what must not interleave with a user's action: the action itself, and the
/// cycle's labels and changes. The backfill runs outside it, so a click never waits for one.
/// </summary>
/// <remarks>Always <see cref="Cycle"/> before <see cref="Action"/>.</remarks>
public sealed class MailboxGates
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _cycles = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _actions = new();

    public SemaphoreSlim Cycle(Guid mailboxId) => _cycles.GetOrAdd(mailboxId, _ => new SemaphoreSlim(1, 1));

    public SemaphoreSlim Action(Guid mailboxId) => _actions.GetOrAdd(mailboxId, _ => new SemaphoreSlim(1, 1));
}
