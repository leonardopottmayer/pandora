using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>A version together with the complete page state it preserves.</summary>
public sealed record PageVersionDto(
    long Number,
    DateTimeOffset CreatedAt,
    string Source,
    long? RestoredFrom,
    PageSnapshot Snapshot)
{
    public static PageVersionDto From(PageVersion version) => new(
        version.Number, version.CreatedAt, version.Source, version.RestoredFrom, version.Snapshot);
}
