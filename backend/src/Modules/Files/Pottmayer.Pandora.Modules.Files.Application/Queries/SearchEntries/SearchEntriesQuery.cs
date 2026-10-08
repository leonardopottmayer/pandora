using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.SearchEntries;

/// <param name="Query">Name fragments separated by spaces; every one must match.</param>
/// <param name="Status"><c>present</c> (the default), <c>missing</c>, <c>excluded</c> or <c>all</c>.</param>
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
    int Skip,
    int Take);

public sealed class SearchEntriesQuery(SearchEntriesInput input)
    : QueryBase<SearchEntriesInput, PageDto<EntryDto>>(input);
