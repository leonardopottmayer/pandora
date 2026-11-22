using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetRecurringTransaction;

public sealed record GetRecurringTransactionInput(Guid UserId, Guid Id);

[RequiresPermission(FinancesPermissions.Recurrences.Read)]
public sealed class GetRecurringTransactionQuery(GetRecurringTransactionInput input)
    : QueryBase<GetRecurringTransactionInput, RecurringTransactionDto>(input);
