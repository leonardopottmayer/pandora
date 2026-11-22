using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.Repositories;

public sealed class MailLabelRepository(IDataContextAccessor accessor)
    : StandardRepository<MailLabel, Guid>(accessor), IMailLabelRepository
{
    public async Task<IReadOnlyList<MailLabel>> ListByMailboxAsync(Guid mailboxId, CancellationToken ct = default) =>
        await Set.Where(l => l.MailboxId == mailboxId)
                 .OrderBy(l => l.Name)
                 .ToListAsync(ct);
}
