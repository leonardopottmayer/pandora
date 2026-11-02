namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// One hit of the full-text search: the minimum the command palette needs to show a row and open it.
/// <see cref="Excerpt"/> is plain text around the match; the palette highlights it without HTML.
/// </summary>
public sealed record PageSearchResultDto(
    Guid Id,
    Guid SpaceId,
    string Title,
    string Slug,
    string? Icon,
    bool IsArchived,
    string Excerpt);
