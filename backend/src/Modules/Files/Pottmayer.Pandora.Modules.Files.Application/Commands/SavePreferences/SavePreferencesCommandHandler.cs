using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.SavePreferences;

/// <summary>Turns Files on or off for the account. The first save also gives the user the built-in filters.</summary>
public sealed class SavePreferencesCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<SavePreferencesCommand, PreferencesDto>
{
    protected override async Task<Result<PreferencesDto>> HandleAsync(SavePreferencesCommand request, CancellationToken ct)
    {
        var input = request.Input;

        await factory.ExecuteAsync(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPreferencesRepository>();
            var preferences = await repo.GetByIdAsync(input.UserId, token);
            if (preferences is not null)
            {
                preferences.SetEnabled(input.IsEnabled);
                return;
            }

            await repo.AddAsync(Preferences.Create(input.UserId, input.IsEnabled), token);
            await ctx.AcquireRepository<IFilterRepository>().AddRangeAsync(Filter.CreateBuiltins(input.UserId), token);
        }, cancellationToken: ct);

        return Ok(new PreferencesDto(input.IsEnabled));
    }
}
