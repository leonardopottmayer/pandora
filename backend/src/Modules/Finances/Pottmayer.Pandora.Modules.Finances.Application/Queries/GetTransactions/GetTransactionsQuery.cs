using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransactions;

public sealed record GetTransactionsInput(
    Guid UserId,
    Guid? AccountId,
    DateOnly? From,
    DateOnly? To,
    string? Kind,
    string? Status,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    string? Text,
    string? Origin,
    IReadOnlyList<Guid>? TagIds,
    int Skip,
    int Take);

public sealed class GetTransactionsQuery(GetTransactionsInput input)
    : QueryBase<GetTransactionsInput, IReadOnlyList<TransactionDto>>(input);
