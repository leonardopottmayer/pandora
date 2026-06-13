using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCard;

public sealed record GetCardInput(Guid UserId, Guid CardId);

public sealed class GetCardQuery(GetCardInput input) : QueryBase<GetCardInput, CardDto>(input);
