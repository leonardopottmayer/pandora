using System.Text.Json.Nodes;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.ImportNote;

public sealed class ImportNoteCommandHandler(ISender sender) : CommandHandlerBase<ImportNoteCommand, Guid>
{
    protected override async Task<Result<Guid>> HandleAsync(ImportNoteCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Title)) return Fail(PageErrors.InvalidTitle);
        if (input.Title.Trim().Length > 255) return Fail(PageErrors.TitleTooLong);
        var blocks = new JsonArray();
        // Imported mail is literal text: markdown-looking lines must not turn into tags or commands.
        foreach (var line in input.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            if (!string.IsNullOrWhiteSpace(line))
                blocks.Add(new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["type"] = "paragraph",
                    ["content"] = new JsonArray(new JsonObject { ["text"] = line }) });
        blocks.Add(new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["type"] = "bookmark",
            ["props"] = new JsonObject { ["url"] = input.SourcePath, ["title"] = input.SourceTitle, ["siteName"] = "Pandora" } });
        var content = new JsonObject { ["version"] = 1, ["blocks"] = blocks }.ToJsonString();
        var result = await sender.Send(new CreatePageCommand(new CreatePageInput(input.UserId,
            input.Title, null, null, content, input.SpaceId, input.FolderId)), ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!.Id);
    }
}
