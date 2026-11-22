using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>A space with what its card shows: how many pages it holds and when one last changed.</summary>
public sealed record SpaceDto(
    Guid Id,
    string Name,
    string? Icon,
    string? Color,
    string? Description,
    bool IsDefault,
    bool IsArchived,
    int PageCount,
    DateTimeOffset? LastEditedAt,
    DateTimeOffset CreatedAt)
{
    public static SpaceDto From(Space s, SpacePageStats? stats) =>
        new(s.Id, s.Name, s.Icon, s.Color, s.Description, s.IsDefault, s.IsArchived,
            stats?.PageCount ?? 0, stats?.LastEditedAt, s.CreatedAt);
}
