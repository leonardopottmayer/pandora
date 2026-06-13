using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetStatement;

public sealed record GetStatementInput(Guid UserId, Guid StatementId);

public sealed class GetStatementQuery(GetStatementInput input)
    : QueryBase<GetStatementInput, CardStatementDetailDto>(input);
