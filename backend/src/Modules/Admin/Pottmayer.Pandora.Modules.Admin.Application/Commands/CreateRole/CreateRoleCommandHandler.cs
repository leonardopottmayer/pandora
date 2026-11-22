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

namespace Pottmayer.Pandora.Modules.Admin.Application.Commands.CreateRole;

public sealed class CreateRoleCommandHandler(IUnitOfWorkFactory factory, IPermissionCatalog catalog, TimeProvider timeProvider)
    : CommandHandlerBase<CreateRoleCommand, Guid>
{
    protected override async Task<Result<Guid>> HandleAsync(CreateRoleCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(AdminErrors.RoleNameRequired);
        var patterns = input.Patterns.Select(p => p.Trim()).Distinct().ToList();
        if (PatternRules.FirstInvalid(patterns, catalog) is { } invalid)
            return Fail(AdminErrors.InvalidPattern(invalid));

        return await factory.ExecuteAsync(AdminModule.DatabaseKey, async (ctx, token) =>
        {
            var roles = ctx.AcquireRepository<IRoleRepository>();
            if (await roles.NameTakenAsync(input.Name, exceptId: null, token))
                return Result<Guid>.Failure(AdminErrors.RoleNameTaken);

            var role = Role.Create(input.Name, input.Description);
            await roles.AddAsync(role, token);
            await ctx.AcquireRepository<IRolePermissionRepository>()
                .AddRangeAsync(patterns.Select(p => RolePermission.Create(role.Id, p)), token);
            await ctx.AcquireRepository<IAuditEntryRepository>().AddAsync(AuditEntry.Record(
                input.UserId, AuditEntry.RoleCreated, AuditEntry.RoleTarget, role.Id.ToString(), before: null,
                JsonSerializer.Serialize(new { name = role.Name, description = role.Description, patterns }), timeProvider), token);
            return Result<Guid>.Success(role.Id);
        }, cancellationToken: ct);
    }
}
