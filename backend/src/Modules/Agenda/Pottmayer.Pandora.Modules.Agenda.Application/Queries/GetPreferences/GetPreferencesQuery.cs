using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetPreferences;

public sealed record GetPreferencesInput(Guid UserId);

public sealed class GetPreferencesQuery(GetPreferencesInput input)
    : QueryBase<GetPreferencesInput, AgendaPreferencesDto>(input);
