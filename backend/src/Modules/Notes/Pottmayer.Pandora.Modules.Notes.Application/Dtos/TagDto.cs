using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// A tag as the filters show it. <see cref="PageCount"/> counts the user's live pages carrying it —
/// a tag can sit at zero when it kept a color after losing its last page.
/// </summary>
public sealed record TagDto(Guid Id, string Slug, string Name, string? Color, int PageCount)
{
    public static TagDto From(Tag t, int pageCount) => new(t.Id, t.Slug, t.Name, t.Color, pageCount);
}

/// <summary>The tags of one page, as carried by <see cref="PageDto"/>. No count — it is this page's list.</summary>
public sealed record PageTagDto(Guid Id, string Slug, string Name, string? Color)
{
    public static PageTagDto From(Tag t) => new(t.Id, t.Slug, t.Name, t.Color);
}
