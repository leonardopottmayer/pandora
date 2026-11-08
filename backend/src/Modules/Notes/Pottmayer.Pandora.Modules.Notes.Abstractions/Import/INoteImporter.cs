using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Abstractions.Import;

/// <summary>Creates an independent note from another module's text and an internal source link.</summary>
public interface INoteImporter
{
    Task<Result<Guid>> ImportAsync(ImportNoteRequest request, CancellationToken ct = default);
}
