using System.Collections.Concurrent;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;

/// <summary>
/// The in-process gates of each mailbox (the same single-instance assumption as
/// <c>ExternalCredentialProvider</c>). <see cref="Cycle"/> keeps the job and "Sync now" from running two cycles at
/// once. <see cref="Action"/> serializes what must not interleave with a user's action: the action itself, and the
/// cycle's labels and changes. The backfill runs outside it, so a click never waits for one.
/// <see cref="Rules"/> keeps a rule's write and the diff of the provider's filters apart: between the provider's answer
/// and the rule's save, the diff would take the new filter for one made in Gmail and import it.
/// </summary>
/// <remarks>Always <see cref="Cycle"/> before <see cref="Action"/> or <see cref="Rules"/>.</remarks>
public sealed class MailboxGates
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _cycles = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _actions = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _rules = new();

    public SemaphoreSlim Cycle(Guid mailboxId) => _cycles.GetOrAdd(mailboxId, _ => new SemaphoreSlim(1, 1));

    public SemaphoreSlim Action(Guid mailboxId) => _actions.GetOrAdd(mailboxId, _ => new SemaphoreSlim(1, 1));

    public SemaphoreSlim Rules(Guid mailboxId) => _rules.GetOrAdd(mailboxId, _ => new SemaphoreSlim(1, 1));
}
