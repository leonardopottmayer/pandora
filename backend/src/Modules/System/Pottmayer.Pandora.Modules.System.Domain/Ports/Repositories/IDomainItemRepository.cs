using Pottmayer.Pandora.Modules.System.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.System.Domain.Ports.Repositories;

public interface IDomainItemRepository : IRepository<DomainItem>
{
    Task<IReadOnlyList<DomainItem>> GetByDomainNameAsync(string domainName, CancellationToken ct = default);
    Task<DomainItem?> FindAsync(string domainName, string itemValue, CancellationToken ct = default);
}
