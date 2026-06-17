using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetRecurringTransactions;

public sealed record GetRecurringTransactionsInput(Guid UserId);

public sealed class GetRecurringTransactionsQuery(GetRecurringTransactionsInput input)
    : QueryBase<GetRecurringTransactionsInput, IReadOnlyList<RecurringTransactionDto>>(input);
