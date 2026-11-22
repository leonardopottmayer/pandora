using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;

public sealed record CreatePageInput(
    Guid UserId,
    string Title,
    Guid? ParentId,
    string? Icon,
    string? Content,
    Guid? SpaceId = null,
    Guid? FolderId = null,
    string Source = PageVersionSource.Create);

/// <summary>
/// Creates a page, optionally as a child of another. The slug is derived from the title and made
/// unique per user; a top-level page may sit in a folder, and lives in its space; the blocks (a <see cref="PageContent"/> document as JSON) default to none. A child lives in its parent's space; a
/// top-level page in the named one, or the user's default.
/// </summary>
[RequiresPermission(NotesPermissions.Pages.Create)]
public sealed class CreatePageCommand(CreatePageInput input)
    : CommandBase<CreatePageInput, PageDto>(input);
