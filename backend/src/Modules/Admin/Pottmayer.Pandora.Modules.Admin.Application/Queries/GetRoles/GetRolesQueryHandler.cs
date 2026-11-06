using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetRoles;

public sealed class GetRolesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    protected override async Task<Result<IReadOnlyList<RoleDto>>> HandleAsync(GetRolesQuery request, CancellationToken ct)
    {
        var roles = await factory.ExecuteAsync(AdminModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IRoleRepository>().ListWithGrantsAsync(token), cancellationToken: ct);

        return Ok(roles.Select(r => new RoleDto(
            r.Role.Id, r.Role.Name, r.Role.Description, r.Role.IsSystem, r.Role.IsDefault, r.Patterns, r.Holders)).ToList());
    }
}
