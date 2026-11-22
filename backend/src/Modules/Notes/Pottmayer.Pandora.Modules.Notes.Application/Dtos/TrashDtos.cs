using System.Text.Json;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>A page in the trash: what was deleted, when, and how many sub-pages went with it.</summary>
public sealed record TrashItemDto(Guid Id, Guid SpaceId, string Title, string? Icon, DateTimeOffset DeletedAt, int SubpageCount);

/// <summary>A page as its public link shows it: no owner, no tree, nothing but the page.</summary>
public sealed record PublicPageDto(string Title, string? Icon, JsonElement Content, PageAppearance Appearance, DateTimeOffset? UpdatedAt)
{
    public static PublicPageDto From(Page p) =>
        new(p.Title, p.Icon, JsonDocument.Parse(p.Content).RootElement.Clone(), PageAppearance.Parse(p.Appearance), p.UpdatedAt ?? p.CreatedAt);
}
