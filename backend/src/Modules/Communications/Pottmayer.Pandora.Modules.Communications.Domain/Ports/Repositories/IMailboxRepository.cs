using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;

public interface IMailboxRepository : IStandardRepository<Mailbox, Guid>
{
    Task<Mailbox?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Mailbox>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Mailbox>> ListByExternalAccountAsync(Guid externalAccountId, CancellationToken ct = default);

    /// <summary>The ids the sync job walks: every mailbox not waiting for the user to reconnect it.</summary>
    Task<IReadOnlyList<Guid>> ListSyncableIdsAsync(CancellationToken ct = default);
}
