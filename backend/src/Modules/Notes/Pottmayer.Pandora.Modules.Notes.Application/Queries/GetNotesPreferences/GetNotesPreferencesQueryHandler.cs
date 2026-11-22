using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetNotesPreferences;

public sealed class GetNotesPreferencesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetNotesPreferencesQuery, NotesPreferencesDto>
{
    protected override async Task<Result<NotesPreferencesDto>> HandleAsync(GetNotesPreferencesQuery request, CancellationToken ct)
    {
        var preferences = await factory.ExecuteAsync(NotesModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<INotesPreferencesRepository>().GetByIdAsync(request.Input.UserId, token), cancellationToken: ct);
        return Ok(new NotesPreferencesDto(preferences?.DailyTemplateId));
    }
}
