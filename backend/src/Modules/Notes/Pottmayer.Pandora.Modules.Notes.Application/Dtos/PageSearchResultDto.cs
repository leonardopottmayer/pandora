namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// One hit of the full-text search: the minimum the command palette needs to show a row and open it.
/// <see cref="Excerpt"/> is a plain slice of the body around the match — no highlighting (v2).
/// </summary>
public sealed record PageSearchResultDto(
    Guid Id,
    string Title,
    string Slug,
    string? Icon,
    bool IsArchived,
    string Excerpt);
