using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransaction;

public sealed record ApprovePendingTransactionInput(Guid UserId, Guid Id);

/// <summary>
/// Converts a pending suggestion into a real, posted transaction and marks the suggestion approved.
/// Works for both import-sourced and recurrence-sourced suggestions, against either an account or a card.
/// </summary>
[RequiresPermission(FinancesPermissions.Pending.Approve)]
public sealed class ApprovePendingTransactionCommand(ApprovePendingTransactionInput input)
    : CommandBase<ApprovePendingTransactionInput, TransactionDto>(input);
