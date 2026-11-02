using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageVersions;

public sealed record GetPageVersionsInput(Guid UserId, Guid PageId, long? Before);

/// <summary>Lists page history metadata without loading the markdown snapshots.</summary>
public sealed class GetPageVersionsQuery(GetPageVersionsInput input)
    : QueryBase<GetPageVersionsInput, IReadOnlyList<PageVersionSummaryDto>>(input);
