using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetUsers;

public sealed class GetUsersQueryHandler(IUnitOfWorkFactory factory, IUserDirectory directory)
    : QueryHandlerBase<GetUsersQuery, IReadOnlyList<AdminUserDto>>
{
    protected override async Task<Result<IReadOnlyList<AdminUserDto>>> HandleAsync(GetUsersQuery request, CancellationToken ct)
    {
        var users = await directory.ListAsync(ct);
        var userRoles = await factory.ExecuteAsync(AdminModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IUserRoleRepository>().ListAllAsync(token), cancellationToken: ct);
        var rolesByUser = userRoles.ToLookup(r => r.UserId, r => r.RoleId);

        return Ok(users.Select(u => AdminUserDtos.From(u, [.. rolesByUser[u.Id]])).ToList());
    }
}
