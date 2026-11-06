using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardStatements;

public sealed record GetCardStatementsInput(Guid UserId, Guid CardId);

[RequiresPermission(FinancesPermissions.Statements.Read)]
public sealed class GetCardStatementsQuery(GetCardStatementsInput input)
    : QueryBase<GetCardStatementsInput, IReadOnlyList<CardStatementDto>>(input);
