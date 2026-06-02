using Pottmayer.Pandora.Modules.Users.Abstractions;
using Pottmayer.Pandora.Modules.Users.Application.Dtos;
using Pottmayer.Pandora.Modules.Users.Domain.Errors;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Users.Application.Queries.GetPreferences;

public sealed class GetPreferencesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPreferencesQuery, UserPreferencesDto>
{
    protected override async Task<Result<UserPreferencesDto>> HandleAsync(
        GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var user = await factory.ExecuteAsync(UsersModule.Name, async (ctx, ct) =>
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
