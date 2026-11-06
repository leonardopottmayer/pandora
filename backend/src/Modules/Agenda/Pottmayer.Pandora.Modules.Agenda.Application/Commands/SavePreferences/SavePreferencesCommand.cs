using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SavePreferences;

public sealed record SavePreferencesInput(Guid UserId, int DefaultAlertOffsetMinutes);

[RequiresPermission(AgendaPermissions.Preferences.Update)]
public sealed class SavePreferencesCommand(SavePreferencesInput input)
    : CommandBase<SavePreferencesInput, AgendaPreferencesDto>(input);
