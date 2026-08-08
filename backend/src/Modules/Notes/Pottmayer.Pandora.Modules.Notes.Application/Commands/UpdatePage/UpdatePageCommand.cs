using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdatePage;

public sealed record UpdatePageInput(
    Guid UserId,
    Guid PageId,
    string Title,
    string? Icon,
    string ContentMarkdown);

/// <summary>
/// Edits a page's title, icon, and markdown body — the autosave path. The slug is intentionally not
/// recomputed: it stays fixed so links to the page survive renames.
/// </summary>
public sealed class UpdatePageCommand(UpdatePageInput input)
    : CommandBase<UpdatePageInput, PageDto>(input);
