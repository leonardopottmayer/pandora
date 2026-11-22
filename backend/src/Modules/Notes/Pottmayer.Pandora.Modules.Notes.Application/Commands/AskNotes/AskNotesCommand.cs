using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.AskNotes;

/// <param name="SearchTerms">Extra words to look for besides the question's own (the Assistant adds related ones).</param>
public sealed record AskNotesInput(Guid UserId, string Question, Guid? SpaceId = null, string? SearchTerms = null);

/// <summary>Answers a question over the owner's saved notes without writing pages or conversations.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
[RequiresPermission(AssistantPermissions.Commands.Run)]
public sealed class AskNotesCommand(AskNotesInput input) : CommandBase<AskNotesInput, NoteAnswerDto>(input);
