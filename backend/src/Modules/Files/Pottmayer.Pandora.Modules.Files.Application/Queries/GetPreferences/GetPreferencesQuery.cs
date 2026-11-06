using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetPreferences;

public sealed record GetPreferencesInput(Guid UserId);

[RequiresPermission(FilesPermissions.Preferences.Read)]
public sealed class GetPreferencesQuery(GetPreferencesInput input)
    : QueryBase<GetPreferencesInput, PreferencesDto>(input);
