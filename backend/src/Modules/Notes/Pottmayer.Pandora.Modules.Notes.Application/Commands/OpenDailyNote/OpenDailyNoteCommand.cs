using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.OpenDailyNote;

public sealed record OpenDailyNoteInput(Guid UserId, DateOnly? Date = null, string Locale = "en");

/// <summary>Finds or creates the user's daily note, defaulting to today in the account's effective zone.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
[RequiresPermission(NotesPermissions.Pages.Create)]
[RequiresPermission(NotesPermissions.Folders.Create)]
[RequiresPermission(NotesPermissions.Spaces.Read)]
public sealed class OpenDailyNoteCommand(OpenDailyNoteInput input) : CommandBase<OpenDailyNoteInput, PageDto>(input);
