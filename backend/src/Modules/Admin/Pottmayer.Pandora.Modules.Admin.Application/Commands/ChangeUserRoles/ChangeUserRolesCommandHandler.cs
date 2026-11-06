using System.Text.Json;
using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Application.Services;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Errors;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.FeatureFlags.Abstractions;
using Pottmayer.Tars.Security.Authorization;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.ChangeUserRoles;

public sealed class ChangeUserRolesCommandHandler(
    IUnitOfWorkFactory factory, IUserDirectory directory, LastAdminGuard guard, IPermissionChecker checker,
    TimeProvider timeProvider)
    : CommandHandlerBase<ChangeUserRolesCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(ChangeUserRolesCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var known = (await directory.ListAsync(ct)).Select(u => u.Id).ToHashSet();
        var targets = input.UserIds.Distinct().ToList();
        if (targets.Any(id => !known.Contains(id)))
            return Fail(AdminErrors.UserNotFound);

        var (role, holders) = await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var found = await ctx.AcquireRepository<IRoleRepository>().GetByIdAsync(input.RoleId, token);
            var ids = found is null ? [] : await ctx.AcquireRepository<IUserRoleRepository>().ListHolderIdsAsync(found.Id, token);
            return (found, ids);
        }, cancellationToken: ct);
        if (role is null)
            return Fail(AdminErrors.RoleNotFound);

        var changed = input.Assign
            ? targets.Where(id => !holders.Contains(id)).ToList()
            : targets.Where(holders.Contains).ToList();
        if (!input.Assign && role.Id == AdminModule.AdminRoleId && !await guard.LeavesAnAdminAsync(holders, changed, ct))
            return Fail(AdminErrors.LastAdmin);

        await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var userRoles = ctx.AcquireRepository<IUserRoleRepository>();
            if (input.Assign)
            {
                await userRoles.AddRangeAsync(changed.Select(id => UserRole.Create(id, role.Id)), token);
            }
            else
            {
                var rows = await userRoles.GetAsync(r => r.RoleId == role.Id && changed.Contains(r.UserId), token);
                await userRoles.RemoveRangeAsync(rows, token);
            }

            var audits = ctx.AcquireRepository<IAuditEntryRepository>();
            var name = JsonSerializer.Serialize(new { role = role.Name });
            foreach (var id in changed)
            {
                await audits.AddAsync(AuditEntry.Record(
                    input.UserId, input.Assign ? AuditEntry.RoleAssigned : AuditEntry.RoleRemoved, AuditEntry.UserTarget,
                    id.ToString(), input.Assign ? null : name, input.Assign ? name : null, timeProvider), token);
            }
            return true;
        }, cancellationToken: ct);

        foreach (var id in changed)
            checker.Invalidate(id.ToString());
        return Ok(changed.Count);
    }
}
