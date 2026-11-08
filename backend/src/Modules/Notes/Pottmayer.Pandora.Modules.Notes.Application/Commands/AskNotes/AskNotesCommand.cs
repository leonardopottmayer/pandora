using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.AskNotes;

public sealed record AskNotesInput(Guid UserId, string Question, Guid? SpaceId = null);

/// <summary>Answers a question over the owner's saved notes without writing pages or conversations.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
[RequiresPermission(AssistantPermissions.Commands.Run)]
public sealed class AskNotesCommand(AskNotesInput input) : CommandBase<AskNotesInput, NoteAnswerDto>(input);
