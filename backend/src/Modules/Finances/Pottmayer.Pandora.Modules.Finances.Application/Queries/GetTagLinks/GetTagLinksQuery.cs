using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTagLinks;

public sealed record GetTagLinksInput(Guid UserId, Guid TagId);

public sealed class GetTagLinksQuery(GetTagLinksInput input)
    : QueryBase<GetTagLinksInput, IReadOnlyList<TagLinkDto>>(input);
