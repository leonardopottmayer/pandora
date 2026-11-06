using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.FeatureFlags.Abstractions;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(
    IUnitOfWorkFactory factory,
    IFeatureFlags featureFlags,
    IPermissionChecker permissions)
    : QueryHandlerBase<GetCurrentUserQuery, CurrentUserDto>
{
    protected override async Task<Result<CurrentUserDto>> HandleAsync(
        GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();
            return await repo.GetByIdAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        if (user is null)
            return Fail(UserErrors.NotFound);

        var features = (await featureFlags.GetAllAsync(cancellationToken))
            .Where(f => f.Enabled)
            .Select(f => f.Definition.Key)
            .ToList();
        var effective = await permissions.GetEffectiveAsync(user.Id.ToString(), cancellationToken);

        return Ok(new CurrentUserDto(
            user.Id.ToString(), user.Name, user.Email.Value, user.Username, features, [.. effective.Order()]));
    }
}
