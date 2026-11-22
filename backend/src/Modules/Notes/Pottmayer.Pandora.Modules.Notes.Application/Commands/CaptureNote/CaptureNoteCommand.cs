using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions.Capture;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CaptureNote;

/// <summary>Appends a dated literal paragraph to the user's capture page, once per inbound event.</summary>
[RequiresPermission(AssistantPermissions.Commands.Run)]
[RequiresPermission(NotesPermissions.Pages.Read)]
[RequiresPermission(NotesPermissions.Pages.Create)]
[RequiresPermission(NotesPermissions.Pages.Update)]
[RequiresPermission(NotesPermissions.Spaces.Read)]
public sealed class CaptureNoteCommand(NoteCaptureRequest input) : CommandBase<NoteCaptureRequest, NoteCaptureResult>(input);
