using Pottmayer.Pandora.Modules.Communications.Application.Commands.OpenMessage;
using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Pandora.Modules.Notes.Abstractions.Import;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SaveMessageToNotes;

public sealed class SaveMessageToNotesCommandHandler(ISender sender, INoteImporter notes)
    : CommandHandlerBase<SaveMessageToNotesCommand, Guid>
{
    protected override async Task<Result<Guid>> HandleAsync(SaveMessageToNotesCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var opened = await sender.Send(new OpenMessageCommand(new OpenMessageInput(input.UserId, input.MessageId)), ct);
        if (opened.IsFailure) return Fail([.. opened.Errors]);
        var mail = opened.Value!;
        var subject = string.IsNullOrWhiteSpace(mail.Message.Subject) ? "Email" : mail.Message.Subject;
        var result = await notes.ImportAsync(new ImportNoteRequest(input.UserId, input.Title ?? subject,
            MailNoteText.Read(mail.TextBody, mail.HtmlBody), $"/mail?message={input.MessageId}", subject, input.SpaceId, input.FolderId), ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }
}
