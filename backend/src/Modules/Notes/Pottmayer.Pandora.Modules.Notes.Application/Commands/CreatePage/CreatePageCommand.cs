using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;

public sealed record CreatePageInput(
    Guid UserId,
    string Title,
    Guid? ParentId,
    string? Icon,
    string? ContentMarkdown);

/// <summary>
/// Creates a page, optionally as a child of another. The slug is derived from the title and made
/// unique per user; the body defaults to empty when omitted.
/// </summary>
public sealed class CreatePageCommand(CreatePageInput input)
    : CommandBase<CreatePageInput, PageDto>(input);
