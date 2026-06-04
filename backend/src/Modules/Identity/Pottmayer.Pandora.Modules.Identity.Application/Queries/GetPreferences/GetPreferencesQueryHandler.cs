using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Pandora.Modules.Identity.Domain.Errors;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.GetPreferences;

public sealed class GetPreferencesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPreferencesQuery, UserPreferencesDto>
{
    protected override async Task<Result<UserPreferencesDto>> HandleAsync(
        GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var user = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();
            return await repo.FindByIdWithPreferencesAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        if (user is null)
            return Fail(UserErrors.NotFound);

        if (user.Preferences is null)
            return Fail(UserErrors.PreferencesNotFound);

        return Ok(new UserPreferencesDto(user.Preferences.Theme.Value));
    }
}
