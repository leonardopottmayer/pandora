using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.PostTransaction;

public sealed record PostTransactionInput(Guid UserId, Guid TransactionId);

/// <summary>Effects a scheduled (pending) transaction immediately. No-op unless it is still pending.</summary>
[RequiresPermission(FinancesPermissions.Transactions.Post)]
public sealed class PostTransactionCommand(PostTransactionInput input)
    : CommandBase<PostTransactionInput, TransactionDto>(input);
