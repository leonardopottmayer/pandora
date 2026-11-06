using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageVersions;

public sealed record GetPageVersionsInput(Guid UserId, Guid PageId, long? Before);

/// <summary>Lists page history metadata without loading the markdown snapshots.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetPageVersionsQuery(GetPageVersionsInput input)
    : QueryBase<GetPageVersionsInput, IReadOnlyList<PageVersionSummaryDto>>(input);
