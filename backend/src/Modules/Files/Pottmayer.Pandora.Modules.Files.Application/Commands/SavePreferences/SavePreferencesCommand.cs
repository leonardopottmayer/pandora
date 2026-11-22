using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.SavePreferences;

public sealed record SavePreferencesInput(Guid UserId, bool IsEnabled);

[RequiresPermission(FilesPermissions.Preferences.Update)]
public sealed class SavePreferencesCommand(SavePreferencesInput input)
    : CommandBase<SavePreferencesInput, PreferencesDto>(input);
