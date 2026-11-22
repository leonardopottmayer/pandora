using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPendingTransactions;

public sealed record GetPendingTransactionsInput(
    Guid UserId,
    PendingTransactionFilter Filter);

[RequiresPermission(FinancesPermissions.Pending.Read)]
public sealed class GetPendingTransactionsQuery(GetPendingTransactionsInput input)
    : QueryBase<GetPendingTransactionsInput, IReadOnlyList<PendingTransactionDto>>(input);
