using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;

public sealed record GetCardsInput(Guid UserId, bool IncludeArchived, IReadOnlyList<Guid>? TagIds = null);

public sealed class GetCardsQuery(GetCardsInput input) : QueryBase<GetCardsInput, IReadOnlyList<CardDto>>(input);
