using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions.Import;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.ImportNote;

/// <summary>Creates a note from literal text and a source bookmark, through normal page creation.</summary>
[RequiresPermission(NotesPermissions.Pages.Create)]
[RequiresPermission(NotesPermissions.Pages.Read)]
[RequiresPermission(NotesPermissions.Spaces.Read)]
[RequiresPermission(NotesPermissions.Folders.Read)]
public sealed class ImportNoteCommand(ImportNoteRequest input) : CommandBase<ImportNoteRequest, Guid>(input);
