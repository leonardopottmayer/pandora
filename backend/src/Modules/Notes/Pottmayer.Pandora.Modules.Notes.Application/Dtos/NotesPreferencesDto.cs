namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>The optional model used only when creating a new daily note.</summary>
public sealed record NotesPreferencesDto(Guid? DailyTemplateId);
