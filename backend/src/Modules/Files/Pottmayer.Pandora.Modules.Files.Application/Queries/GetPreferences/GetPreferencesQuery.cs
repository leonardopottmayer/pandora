using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetPreferences;

public sealed record GetPreferencesInput(Guid UserId);

public sealed class GetPreferencesQuery(GetPreferencesInput input)
    : QueryBase<GetPreferencesInput, PreferencesDto>(input);
