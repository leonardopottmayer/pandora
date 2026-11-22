using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetStatement;

public sealed record GetStatementInput(Guid UserId, Guid StatementId);

[RequiresPermission(FinancesPermissions.Statements.Read)]
public sealed class GetStatementQuery(GetStatementInput input)
    : QueryBase<GetStatementInput, CardStatementDetailDto>(input);
