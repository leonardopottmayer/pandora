using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.Repositories;

public sealed class RoleRepository(IDataContextAccessor accessor)
    : StandardRepository<Role, Guid>(accessor), IRoleRepository
{
    public Task<Role?> FindDefaultAsync(CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(r => r.IsDefault, ct);

    public async Task<IReadOnlyList<RoleWithGrants>> ListWithGrantsAsync(CancellationToken ct = default)
    {
        var roles = await Queryable().AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var patterns = await DbContext.Set<RolePermission>().AsNoTracking().ToListAsync(ct);
        var holders = await DbContext.Set<UserRole>().GroupBy(u => u.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RoleId, g => g.Count, ct);

        return [.. roles.Select(r => new RoleWithGrants(
            r,
            [.. patterns.Where(p => p.RoleId == r.Id).Select(p => p.Pattern).Order(StringComparer.Ordinal)],
            holders.GetValueOrDefault(r.Id)))];
    }

    public Task<bool> NameTakenAsync(string name, Guid? exceptId, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return Queryable().AnyAsync(r => r.Name.ToLower() == normalized && r.Id != exceptId, ct);
    }
}
