using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.BrowseEntries;

/// <param name="ParentPath">The folder to list; null is the root.</param>
public sealed record BrowseEntriesInput(Guid UserId, Guid RootId, string? ParentPath, int Skip, int Take);

public sealed class BrowseEntriesQuery(BrowseEntriesInput input)
    : QueryBase<BrowseEntriesInput, PageDto<EntryDto>>(input);
