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
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetUser;

public sealed class GetUserQueryHandler(IUnitOfWorkFactory factory, IUserDirectory directory, IPermissionCatalog catalog)
    : QueryHandlerBase<GetUserQuery, AdminUserDetailDto>
{
    protected override async Task<Result<AdminUserDetailDto>> HandleAsync(GetUserQuery request, CancellationToken ct)
    {
        var targetId = request.Input.TargetUserId;
        if (await directory.FindAsync(targetId, ct) is not { } user)
            return Fail(AdminErrors.UserNotFound);

        var (roleIds, roles, exceptions) = await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var held = await ctx.AcquireRepository<IUserRoleRepository>().ListForUserAsync(targetId, token);
            var all = await ctx.AcquireRepository<IRoleRepository>().ListWithGrantsAsync(token);
            var heldIds = held.Select(r => r.RoleId).ToHashSet();
            var own = await ctx.AcquireRepository<IUserPermissionRepository>().ListForUserAsync(targetId, token);
            return (heldIds, all.Where(r => heldIds.Contains(r.Role.Id)).ToList(), own);
        }, cancellationToken: ct);

        var allows = exceptions.Where(e => e.Effect == UserPermission.Allow).Select(e => e.Pattern).ToList();
        var denies = exceptions.Where(e => e.Effect == UserPermission.Deny).Select(e => e.Pattern).ToList();

        var permissions = new List<EffectivePermissionDto>();
        foreach (var key in catalog.Definitions.Select(d => d.Key))
        {
            var viaRoles = roles.Where(r => r.Patterns.Any(p => PermissionPatterns.Matches(p, key))).Select(r => r.Role.Name).ToList();
            var allowed = allows.Any(p => PermissionPatterns.Matches(p, key));
            var denied = denies.Any(p => PermissionPatterns.Matches(p, key));
            if (viaRoles.Count > 0 || allowed || denied)
                permissions.Add(new EffectivePermissionDto(key, (viaRoles.Count > 0 || allowed) && !denied, viaRoles, allowed, denied));
        }

        return Ok(new AdminUserDetailDto(
            AdminUserDtos.From(user, [.. roleIds]),
            [.. exceptions.OrderBy(e => e.Pattern, StringComparer.Ordinal).Select(e => new UserExceptionDto(e.Pattern, e.Effect))],
            permissions));
    }
}
