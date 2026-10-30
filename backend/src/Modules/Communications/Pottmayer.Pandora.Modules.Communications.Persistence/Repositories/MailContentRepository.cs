using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.Repositories;

public sealed class MailContentRepository(IDataContextAccessor accessor)
    : StandardRepository<MailContent, Guid>(accessor), IMailContentRepository
{
    public Task<int> DeleteNotAccessedSinceAsync(DateTimeOffset cutoff, CancellationToken ct = default) =>
        Set.Where(c => c.LastAccessedAt < cutoff).ExecuteDeleteAsync(ct);
}
