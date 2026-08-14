using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetBacklinks;

public sealed record GetBacklinksInput(Guid UserId, Guid PageId);

public sealed class GetBacklinksQuery(GetBacklinksInput input)
    : QueryBase<GetBacklinksInput, IReadOnlyList<BacklinkDto>>(input);
