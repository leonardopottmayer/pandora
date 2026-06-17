using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetRecurringTransaction;

public sealed record GetRecurringTransactionInput(Guid UserId, Guid Id);

public sealed class GetRecurringTransactionQuery(GetRecurringTransactionInput input)
    : QueryBase<GetRecurringTransactionInput, RecurringTransactionDto>(input);
