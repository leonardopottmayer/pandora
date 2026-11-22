using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.Repositories;

public sealed class MailboxRepository(IDataContextAccessor accessor)
    : StandardRepository<Mailbox, Guid>(accessor), IMailboxRepository
{
    public Task<Mailbox?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);

    public async Task<IReadOnlyList<Mailbox>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Set.Where(m => m.UserId == userId)
                 .OrderBy(m => m.CreatedAt)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Mailbox>> ListByExternalAccountAsync(Guid externalAccountId, CancellationToken ct = default) =>
        await Set.Where(m => m.ExternalAccountId == externalAccountId).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListSyncableIdsAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
                 .Where(m => m.Status != MailboxStatus.NeedsReconnect)
                 .OrderBy(m => m.CreatedAt)
                 .Select(m => m.Id)
                 .ToListAsync(ct);
}
