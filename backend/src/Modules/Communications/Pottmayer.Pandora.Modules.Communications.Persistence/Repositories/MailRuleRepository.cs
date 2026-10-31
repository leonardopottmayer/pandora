using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.Repositories;

public sealed class MailRuleRepository(IDataContextAccessor accessor)
    : StandardRepository<MailRule, Guid>(accessor), IMailRuleRepository
{
    public async Task<IReadOnlyList<MailRule>> ListByMailboxAsync(Guid mailboxId, CancellationToken ct = default) =>
        await Set.Where(r => r.MailboxId == mailboxId)
                 .OrderBy(r => r.Position)
                 .ToListAsync(ct);
}
