using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.SavePreferences;

public sealed record SavePreferencesInput(Guid UserId, bool IsEnabled);

public sealed class SavePreferencesCommand(SavePreferencesInput input)
    : CommandBase<SavePreferencesInput, PreferencesDto>(input);
