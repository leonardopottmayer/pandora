namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// A page that mentions the one being read ("linked mention"), plus how it mentions it
/// (<c>wikilink</c> or <c>embed</c>). A page that both links and embeds shows up once per kind.
/// </summary>
public sealed record BacklinkDto(
    Guid PageId,
    string Title,
    string Slug,
    string? Icon,
    bool IsArchived,
    string Kind);
