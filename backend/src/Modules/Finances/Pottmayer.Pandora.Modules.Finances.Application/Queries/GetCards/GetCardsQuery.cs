using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;

public sealed record GetCardsInput(Guid UserId, bool IncludeArchived, IReadOnlyList<Guid>? TagIds = null);

[RequiresPermission(FinancesPermissions.Cards.Read)]
public sealed class GetCardsQuery(GetCardsInput input) : QueryBase<GetCardsInput, IReadOnlyList<CardDto>>(input);
