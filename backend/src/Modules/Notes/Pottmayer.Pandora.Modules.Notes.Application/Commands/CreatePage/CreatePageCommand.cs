using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;

public sealed record CreatePageInput(
    Guid UserId,
    string Title,
    Guid? ParentId,
    string? Icon,
    string? ContentMarkdown,
    Guid? SpaceId = null);

/// <summary>
/// Creates a page, optionally as a child of another. The slug is derived from the title and made
/// unique per user; the body defaults to empty when omitted. A child lives in its parent's space; a
/// top-level page in the named one, or the user's default.
/// </summary>
public sealed class CreatePageCommand(CreatePageInput input)
    : CommandBase<CreatePageInput, PageDto>(input);
