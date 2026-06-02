using Pottmayer.Pandora.Modules.Users.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Users.Application.Queries.GetPreferences;

public sealed record GetPreferencesInput(Guid UserId);

public sealed class GetPreferencesQuery(GetPreferencesInput input)
    : QueryBase<GetPreferencesInput, UserPreferencesDto>(input);
