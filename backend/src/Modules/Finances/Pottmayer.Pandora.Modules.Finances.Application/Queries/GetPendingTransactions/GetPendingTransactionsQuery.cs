using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPendingTransactions;

public sealed record GetPendingTransactionsInput(
    Guid UserId,
    PendingTransactionFilter Filter);

public sealed class GetPendingTransactionsQuery(GetPendingTransactionsInput input)
    : QueryBase<GetPendingTransactionsInput, IReadOnlyList<PendingTransactionDto>>(input);
