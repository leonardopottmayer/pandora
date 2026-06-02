using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.System.Domain.Entities;
using Pottmayer.Pandora.Modules.System.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.System.Persistence.Repositories;

public sealed class DomainItemRepository(IDataContextAccessor accessor)
    : RepositoryBase(accessor), IDomainItemRepository
{
    public async Task<IReadOnlyList<DomainItem>> GetByDomainNameAsync(
        string domainName, CancellationToken ct = default)
        => await DbContext.Set<DomainItem>()
            .Where(d => d.DomainName == domainName)
            .ToListAsync(ct);

    public Task<DomainItem?> FindAsync(
        string domainName, string itemValue, CancellationToken ct = default)
        => DbContext.Set<DomainItem>()
            .FirstOrDefaultAsync(d => d.DomainName == domainName && d.ItemValue == itemValue, ct);
}
