using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>History list entry without the potentially large markdown snapshot.</summary>
public sealed record PageVersionSummaryDto(
    long Number,
    DateTimeOffset CreatedAt,
    string Source,
    long? RestoredFrom)
{
    public static PageVersionSummaryDto From(PageVersionInfo version) =>
        new(version.Number, version.CreatedAt, version.Source, version.RestoredFrom);
}
