using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncFilters;

/// <summary>
/// Brings a mailbox's rules in line with the provider's filters, which are the source of truth. Connecting a mailbox
/// needs nothing else: on its first cycle every filter is new. Succeeds with false when the mailbox is gone.
/// </summary>
public sealed record SyncFiltersInput(Guid MailboxId);

public sealed class SyncFiltersCommand(SyncFiltersInput input)
    : CommandBase<SyncFiltersInput, bool>(input);
