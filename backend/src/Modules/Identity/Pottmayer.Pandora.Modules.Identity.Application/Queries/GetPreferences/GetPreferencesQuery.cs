using Pottmayer.Pandora.Modules.Identity.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Identity.Application.Queries.GetPreferences;

public sealed record GetPreferencesInput(Guid UserId);

public sealed class GetPreferencesQuery(GetPreferencesInput input)
    : QueryBase<GetPreferencesInput, UserPreferencesDto>(input);
