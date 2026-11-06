using System.Text.Json;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Bootstrap;

/// <summary>
/// The role grants the system makes on its own: the default role for a new account and the
/// <c>admin</c> role for the bootstrap e-mails. Idempotent; each grant is audited with no actor and
/// drops the user's cached permissions.
/// </summary>
public sealed class RoleAssigner(IUnitOfWorkFactory factory, IPermissionChecker checker, TimeProvider timeProvider)
{
    public Task AssignDefaultAsync(Guid userId, CancellationToken ct = default) =>
        AssignAsync(userId, roleId: null, ct);

    public Task AssignAdminAsync(Guid userId, CancellationToken ct = default) =>
        AssignAsync(userId, AdminModule.AdminRoleId, ct);

    /// <param name="roleId">The role to grant, or <c>null</c> for the default role.</param>
    private async Task AssignAsync(Guid userId, Guid? roleId, CancellationToken ct)
    {
        var assigned = await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var role = roleId is { } id
                ? await ctx.AcquireRepository<IRoleRepository>().GetByIdAsync(id, token)
                : await ctx.AcquireRepository<IRoleRepository>().FindDefaultAsync(token);
            if (role is null)
                return false;

            var userRoles = ctx.AcquireRepository<IUserRoleRepository>();
            if (await userRoles.ExistsAsync(userId, role.Id, token))
                return false;

            await userRoles.AddAsync(UserRole.Create(userId, role.Id), token);
            await ctx.AcquireRepository<IAuditEntryRepository>().AddAsync(AuditEntry.Record(
                actorId: null, AuditEntry.RoleAssigned, AuditEntry.UserTarget, userId.ToString(),
                before: null, after: JsonSerializer.Serialize(new { role = role.Name }), timeProvider), token);
            return true;
        }, cancellationToken: ct);

        if (assigned)
            checker.Invalidate(userId.ToString());
    }
}
