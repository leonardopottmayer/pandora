using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdatePage;

public sealed record UpdatePageInput(
    Guid UserId,
    Guid PageId,
    string Title,
    string? Icon,
    string? Content,
    string Source = PageVersionSource.Edit);

/// <summary>
/// Edits a page's title, icon, and blocks (a <see cref="PageContent"/> document as JSON) — the autosave path. The slug is intentionally not
/// recomputed: it stays fixed so links to the page survive renames.
/// </summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class UpdatePageCommand(UpdatePageInput input)
    : CommandBase<UpdatePageInput, PageDto>(input);
