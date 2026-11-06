using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.BrowseEntries;

/// <param name="ParentPath">The folder to list; null is the root.</param>
public sealed record BrowseEntriesInput(Guid UserId, Guid RootId, string? ParentPath, int Skip, int Take);

[RequiresPermission(FilesPermissions.Entries.Read)]
public sealed class BrowseEntriesQuery(BrowseEntriesInput input)
    : QueryBase<BrowseEntriesInput, PageDto<EntryDto>>(input);
