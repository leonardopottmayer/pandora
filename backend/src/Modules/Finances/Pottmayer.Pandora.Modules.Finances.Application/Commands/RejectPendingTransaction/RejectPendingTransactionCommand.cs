using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RejectPendingTransaction;

public sealed record RejectPendingTransactionInput(Guid UserId, Guid Id, string? Reason);

/// <summary>Dismisses a pending suggestion without creating any transaction. Fails if already decided.</summary>
[RequiresPermission(FinancesPermissions.Pending.Reject)]
public sealed class RejectPendingTransactionCommand(RejectPendingTransactionInput input)
    : CommandBase<RejectPendingTransactionInput, PendingTransactionDto>(input);
