#nullable disable
using System.Linq.Expressions;
using Pottmayer.Pandora.Modules.Identity.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.Query;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IUserRepository"/>. Only the members exercised by the
/// authentication/sign-up flows are implemented.
/// </summary>
internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users;

    public FakeUserRepository(params User[] seed) => _users = [.. seed];

    public IReadOnlyList<User> Users => _users;

    public Task<User> FindByEmailAsync(Email email, CancellationToken ct = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Email == email));

    public Task<User> FindByUsernameAsync(string username, CancellationToken ct = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Username == username.ToLowerInvariant()));

    public Task<User> FindByIdWithPreferencesAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User> AddAsync(User entity, CancellationToken ct = default)
    {
        _users.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<User> UpdateAsync(User entity, CancellationToken ct = default)
        => Task.FromResult(entity);

    public Task<User> GetByIdAsync(Guid key, CancellationToken ct = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == key));

    // --- Unused IStandardRepository surface ---
    public IQueryable<User> Queryable(Expression<Func<User, bool>> predicate = null) => throw new NotImplementedException();
    public Task<IEnumerable<User>> GetAsync(Expression<Func<User, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<User> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRangeAsync(IEnumerable<User> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User> RemoveByKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User> RemoveAsync(User entity, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRangeAsync(IEnumerable<User> entities, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsKeyAsync(Guid key, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAsync(Expression<Func<User, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> CountAsync(Expression<Func<User, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyAsync(Expression<Func<User, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize, Expression<Func<User, bool>> predicate = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<DataQueryResult<User>> ExecuteQueryAsync(QueryParams queryParams, CancellationToken ct = default) => throw new NotImplementedException();
}
