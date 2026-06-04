using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;

public interface IUserRepository : IStandardRepository<User, Guid>
{
    Task<User?> FindByEmailAsync(Email email, CancellationToken ct = default);
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> FindByIdWithPreferencesAsync(Guid id, CancellationToken ct = default);
}
