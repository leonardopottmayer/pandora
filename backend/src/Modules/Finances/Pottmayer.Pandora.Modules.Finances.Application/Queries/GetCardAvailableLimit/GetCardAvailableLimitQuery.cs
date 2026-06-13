using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardAvailableLimit;

public sealed record GetCardAvailableLimitInput(Guid UserId, Guid CardId);

public sealed class GetCardAvailableLimitQuery(GetCardAvailableLimitInput input)
    : QueryBase<GetCardAvailableLimitInput, CardAvailableLimitDto>(input);
