using Pottmayer.Pandora.Modules.Users.Abstractions;
using Pottmayer.Pandora.Modules.Users.Application.Dtos;
using Pottmayer.Pandora.Modules.Users.Domain.Errors;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Users.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Users.Application.Commands.UpsertPreferences;

public sealed class UpsertPreferencesCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UpsertPreferencesCommand, UserPreferencesDto>
{
    protected override async Task<Result<UserPreferencesDto>> HandleAsync(
        UpsertPreferencesCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!AppTheme.IsSupported(input.Theme))
            return Fail(UserErrors.InvalidTheme(input.Theme));

        var theme = AppTheme.FromValue(input.Theme);

        var user = await factory.ExecuteAsync(UsersModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();

            var user = await repo.FindByIdWithPreferencesAsync(input.UserId, token);
            if (user is null)
                return null;

            user.UpdatePreferences(theme);
            await repo.UpdateAsync(user, token);
            return user;
        }, cancellationToken: ct);

        if (user is null)
            return Fail(UserErrors.NotFound);

        return Ok(new UserPreferencesDto(user.Preferences!.Theme.Value));
    }
}
