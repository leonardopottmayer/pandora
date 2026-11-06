using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransaction;

public sealed record GetTransactionInput(Guid UserId, Guid Id);

[RequiresPermission(FinancesPermissions.Transactions.Read)]
public sealed class GetTransactionQuery(GetTransactionInput input)
    : QueryBase<GetTransactionInput, TransactionDto>(input);
