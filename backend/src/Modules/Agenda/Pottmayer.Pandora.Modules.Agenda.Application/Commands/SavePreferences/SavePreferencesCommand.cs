using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SavePreferences;

public sealed record SavePreferencesInput(Guid UserId, int DefaultAlertOffsetMinutes);

public sealed class SavePreferencesCommand(SavePreferencesInput input)
    : CommandBase<SavePreferencesInput, AgendaPreferencesDto>(input);
