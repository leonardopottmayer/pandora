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

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.UpdateRole;

/// <summary>A role's patterns reach every holder, so every cached subject is dropped.</summary>
public sealed class UpdateRoleCommandHandler(
    IUnitOfWorkFactory factory, IPermissionCatalog catalog, IPermissionChecker checker, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateRoleCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(UpdateRoleCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(AdminErrors.RoleNameRequired);
        var patterns = input.Patterns.Select(p => p.Trim()).Distinct().ToList();
        if (PatternRules.FirstInvalid(patterns, catalog) is { } invalid)
            return Fail(AdminErrors.InvalidPattern(invalid));

        var result = await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var roles = ctx.AcquireRepository<IRoleRepository>();
            var role = await roles.GetByIdAsync(input.RoleId, token);
            if (role is null)
                return Result<bool>.Failure(AdminErrors.RoleNotFound);
            if (role.IsSystem)
                return Result<bool>.Failure(AdminErrors.SystemRole);
            if (await roles.NameTakenAsync(input.Name, role.Id, token))
                return Result<bool>.Failure(AdminErrors.RoleNameTaken);

            var grants = ctx.AcquireRepository<IRolePermissionRepository>();
            var current = await grants.ListForRoleAsync(role.Id, token);
            var before = JsonSerializer.Serialize(new
            {
                name = role.Name, description = role.Description,
                patterns = current.Select(p => p.Pattern).Order(StringComparer.Ordinal),
            });

            role.Rename(input.Name, input.Description);
            await roles.UpdateAsync(role, token);
            await grants.RemoveRangeAsync(current.Where(p => !patterns.Contains(p.Pattern)), token);
            await grants.AddRangeAsync(
                patterns.Where(p => current.All(c => c.Pattern != p)).Select(p => RolePermission.Create(role.Id, p)), token);

            await ctx.AcquireRepository<IAuditEntryRepository>().AddAsync(AuditEntry.Record(
                input.UserId, AuditEntry.RoleUpdated, AuditEntry.RoleTarget, role.Id.ToString(), before,
                JsonSerializer.Serialize(new { name = role.Name, description = role.Description, patterns = patterns.Order(StringComparer.Ordinal) }),
                timeProvider), token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        if (result.IsSuccess)
            checker.InvalidateAll();
        return result;
    }
}
