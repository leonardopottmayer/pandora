using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardStatements;

public sealed record GetCardStatementsInput(Guid UserId, Guid CardId);

public sealed class GetCardStatementsQuery(GetCardStatementsInput input)
    : QueryBase<GetCardStatementsInput, IReadOnlyList<CardStatementDto>>(input);
