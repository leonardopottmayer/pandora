using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTagLinks;

public sealed record GetTagLinksInput(Guid UserId, Guid TagId);

[RequiresPermission(FinancesPermissions.Tags.Read)]
public sealed class GetTagLinksQuery(GetTagLinksInput input)
    : QueryBase<GetTagLinksInput, IReadOnlyList<TagLinkDto>>(input);
