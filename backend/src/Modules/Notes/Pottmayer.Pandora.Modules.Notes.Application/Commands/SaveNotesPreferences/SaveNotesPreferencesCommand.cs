using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SaveNotesPreferences;

public sealed record SaveNotesPreferencesInput(Guid UserId, Guid? DailyTemplateId);

/// <summary>Chooses an owned active model for new daily notes, or clears that preference.</summary>
[RequiresPermission(NotesPermissions.Preferences.Update)]
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class SaveNotesPreferencesCommand(SaveNotesPreferencesInput input)
    : CommandBase<SaveNotesPreferencesInput, NotesPreferencesDto>(input);
