namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// One page as a node of the wiki graph. <see cref="Degree"/> counts the edges touching it
/// <em>within this graph</em> — the frontend sizes nodes by it, so it has to follow the same
/// filtering the rest of the payload went through.
/// </summary>
public sealed record GraphNodeDto(
    Guid Id,
    string Title,
    string Slug,
    string? Icon,
    bool IsArchived,
    int Degree);
