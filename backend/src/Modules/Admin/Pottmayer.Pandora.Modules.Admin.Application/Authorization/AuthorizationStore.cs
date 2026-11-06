using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Authorization;

/// <summary>
/// The grants of a user, read from <c>adm003</c>–<c>adm005</c> for the Tars permission checker, which
/// expands and caches them. Subjects are user ids; anything else holds nothing.
/// </summary>
public sealed class AuthorizationStore(IUnitOfWorkFactory factory) : IAuthorizationStore
{
    public async Task<AuthorizationGrants> GetGrantsAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(subjectId, out var userId))
            return AuthorizationGrants.None;

        return await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, ct) =>
        {
            var patterns = await ctx.AcquireRepository<IUserRoleRepository>().ListPatternsAsync(userId, ct);
            var exceptions = await ctx.AcquireRepository<IUserPermissionRepository>().ListForUserAsync(userId, ct);

            return new AuthorizationGrants(
                patterns,
                [.. exceptions.Where(e => e.Effect == UserPermission.Allow).Select(e => e.Pattern)],
                [.. exceptions.Where(e => e.Effect == UserPermission.Deny).Select(e => e.Pattern)]);
        }, cancellationToken: cancellationToken);
    }
}
