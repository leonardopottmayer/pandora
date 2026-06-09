using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetCurrentUserQuery, CurrentUserDto>
{
    protected override async Task<Result<CurrentUserDto>> HandleAsync(
        GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();
            return await repo.GetByIdAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        if (user is null)
            return Fail(UserErrors.NotFound);

        return Ok(new CurrentUserDto(user.Id.ToString(), user.Name, user.Email.Value, user.Username));
    }
}
