using Pottmayer.Pandora.Modules.Notes.Abstractions.Import;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.ImportNote;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>Routes module imports through the authorized Notes creation pipeline.</summary>
public sealed class NoteImporter(ISender sender) : INoteImporter
{
    public async Task<Result<Guid>> ImportAsync(ImportNoteRequest request, CancellationToken ct = default) =>
        await sender.Send(new ImportNoteCommand(request), ct);
}
