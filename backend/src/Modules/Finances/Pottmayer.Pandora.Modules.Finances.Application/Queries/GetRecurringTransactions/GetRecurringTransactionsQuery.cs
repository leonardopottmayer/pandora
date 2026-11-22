using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetRecurringTransactions;

public sealed record GetRecurringTransactionsInput(Guid UserId);

[RequiresPermission(FinancesPermissions.Recurrences.Read)]
public sealed class GetRecurringTransactionsQuery(GetRecurringTransactionsInput input)
    : QueryBase<GetRecurringTransactionsInput, IReadOnlyList<RecurringTransactionDto>>(input);
