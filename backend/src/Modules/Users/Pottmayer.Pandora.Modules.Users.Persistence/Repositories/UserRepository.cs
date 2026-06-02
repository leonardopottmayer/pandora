using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Users.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Users.Persistence.Repositories;

public sealed class UserRepository(IDataContextAccessor accessor)
    : StandardRepository<User, Guid>(accessor), IUserRepository
{
    public Task<User?> FindByEmailAsync(Email email, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(u => u.Username == username.ToLowerInvariant(), ct);

    public Task<User?> FindByIdWithPreferencesAsync(Guid id, CancellationToken ct = default)
        => Queryable().Include(u => u.Preferences).FirstOrDefaultAsync(u => u.Id == id, ct);
}
