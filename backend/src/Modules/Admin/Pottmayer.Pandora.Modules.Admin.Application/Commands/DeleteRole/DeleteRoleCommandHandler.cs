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

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.DeleteRole;

public sealed class DeleteRoleCommandHandler(IUnitOfWorkFactory factory, IPermissionChecker checker, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteRoleCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteRoleCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var roles = ctx.AcquireRepository<IRoleRepository>();
            var role = await roles.GetByIdAsync(input.RoleId, token);
            if (role is null)
                return Result<bool>.Failure(AdminErrors.RoleNotFound);
            if (role.IsSystem)
                return Result<bool>.Failure(AdminErrors.SystemRole);
            if (role.IsDefault)
                return Result<bool>.Failure(AdminErrors.DefaultRole);

            var patterns = await ctx.AcquireRepository<IRolePermissionRepository>().ListForRoleAsync(role.Id, token);
            // The role's patterns and holders go with it (ON DELETE CASCADE on adm003 and adm004).
            await roles.RemoveAsync(role, token);
            await ctx.AcquireRepository<IAuditEntryRepository>().AddAsync(AuditEntry.Record(
                input.UserId, AuditEntry.RoleDeleted, AuditEntry.RoleTarget, role.Id.ToString(),
                JsonSerializer.Serialize(new { name = role.Name, description = role.Description, patterns = patterns.Select(p => p.Pattern) }),
                after: null, timeProvider), token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        if (result.IsSuccess)
            checker.InvalidateAll();
        return result;
    }
}
