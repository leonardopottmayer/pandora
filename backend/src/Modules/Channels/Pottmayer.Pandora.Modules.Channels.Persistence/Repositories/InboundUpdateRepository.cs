using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.Repositories;

public sealed class InboundUpdateRepository(IDataContextAccessor accessor)
    : StandardRepository<InboundUpdate, Guid>(accessor), IInboundUpdateRepository
{
    public Task<bool> ExistsAsync(string provider, long providerUpdateId, CancellationToken ct = default) =>
        Queryable().AnyAsync(u => u.Provider == provider && u.ProviderUpdateId == providerUpdateId, ct);

    public async Task<long?> GetLastUpdateIdAsync(string provider, CancellationToken ct = default) =>
        await Queryable()
            .Where(u => u.Provider == provider)
            .OrderByDescending(u => u.ProviderUpdateId)
            .Select(u => (long?)u.ProviderUpdateId)
            .FirstOrDefaultAsync(ct);

    public Task<int> PurgeRawOlderThanAsync(DateTimeOffset receivedBefore, CancellationToken ct = default) =>
        Queryable()
            .Where(u => u.ReceivedAt < receivedBefore && u.Raw != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.Raw, (string?)null), ct);
}
