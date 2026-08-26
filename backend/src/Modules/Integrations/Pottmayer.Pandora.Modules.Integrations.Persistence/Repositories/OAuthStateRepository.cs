using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Integrations.Persistence.Repositories;

public sealed class OAuthStateRepository(IDataContextAccessor accessor)
    : StandardRepository<OAuthState, Guid>(accessor), IOAuthStateRepository
{
    public Task<OAuthState?> FindByStateAsync(string state, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(s => s.State == state, ct);
}
