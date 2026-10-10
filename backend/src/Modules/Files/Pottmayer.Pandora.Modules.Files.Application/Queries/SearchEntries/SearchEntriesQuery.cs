using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.SearchEntries;

/// <param name="Query">Fragments of the name, title, artist or album, separated by spaces; every one must match.</param>
/// <param name="Status"><c>present</c> (the default), <c>missing</c>, <c>excluded</c> or <c>all</c>.</param>
/// <param name="TakenFrom">Photos taken on or after this day, by the camera's clock.</param>
/// <param name="TakenTo">Photos taken on or before this day.</param>
/// <param name="Resolution"><c>sd</c>, <c>hd</c>, <c>full-hd</c> or <c>4k</c>, by the shorter side.</param>
/// <param name="MinDuration">Seconds.</param>
public sealed record SearchEntriesInput(
    Guid UserId,
    string? Query,
    Guid? DeviceId,
    Guid? RootId,
    string? Category,
    long? MinSize,
    long? MaxSize,
    DateTimeOffset? ModifiedFrom,
    DateTimeOffset? ModifiedTo,
    string? Status,
    DateOnly? TakenFrom,
    DateOnly? TakenTo,
    string? Resolution,
    int? MinDuration,
    int? MaxDuration,
    int Skip,
    int Take);

public sealed class SearchEntriesQuery(SearchEntriesInput input)
    : QueryBase<SearchEntriesInput, PageDto<EntryDto>>(input);
