using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransaction;

public sealed record GetTransactionInput(Guid UserId, Guid Id);

public sealed class GetTransactionQuery(GetTransactionInput input)
    : QueryBase<GetTransactionInput, TransactionDto>(input);
