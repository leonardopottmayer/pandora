using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetPreferences;

/// <summary>The account switch; off when never saved.</summary>
public sealed class GetPreferencesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPreferencesQuery, PreferencesDto>
{
    protected override async Task<Result<PreferencesDto>> HandleAsync(GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var preferences = await factory.ExecuteAsync(FilesModule.DatabaseKey, (ctx, ct) =>
            ctx.AcquireRepository<IPreferencesRepository>().GetByIdAsync(request.Input.UserId, ct),
            cancellationToken: cancellationToken);

        return Ok(new PreferencesDto(preferences?.IsEnabled ?? false));
    }
}
