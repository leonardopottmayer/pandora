using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SaveMessageToNotes;

public sealed record SaveMessageToNotesInput(Guid UserId, Guid MessageId, string? Title, Guid? SpaceId, Guid? FolderId);

/// <summary>Copies an owned message's text to Notes, with a link back to the message.</summary>
[RequiresPermission(CommunicationsPermissions.Messages.Read)]
[RequiresPermission(NotesPermissions.Pages.Create)]
[RequiresPermission(NotesPermissions.Pages.Read)]
[RequiresPermission(NotesPermissions.Spaces.Read)]
[RequiresPermission(NotesPermissions.Folders.Read)]
public sealed class SaveMessageToNotesCommand(SaveMessageToNotesInput input) : CommandBase<SaveMessageToNotesInput, Guid>(input);
