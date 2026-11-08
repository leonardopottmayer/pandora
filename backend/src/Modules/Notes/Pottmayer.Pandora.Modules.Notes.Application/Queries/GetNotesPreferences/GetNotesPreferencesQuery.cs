using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetNotesPreferences;

public sealed record GetNotesPreferencesInput(Guid UserId);

/// <summary>Reads the user's Notes preferences; a missing row means no daily template.</summary>
[RequiresPermission(NotesPermissions.Preferences.Read)]
public sealed class GetNotesPreferencesQuery(GetNotesPreferencesInput input)
    : QueryBase<GetNotesPreferencesInput, NotesPreferencesDto>(input);
