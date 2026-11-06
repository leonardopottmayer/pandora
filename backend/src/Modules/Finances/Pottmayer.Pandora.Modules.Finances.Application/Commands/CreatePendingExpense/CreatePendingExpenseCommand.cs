using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreatePendingExpense;

/// <summary>
/// An expense the user typed (e.g. through the assistant), paid from exactly one of
/// <see cref="AccountId"/> or <see cref="CardId"/>. It lands in the inbox as a <c>manual</c> pending
/// transaction in the target's currency, and becomes a real transaction only when approved.
/// </summary>
public sealed record CreatePendingExpenseInput(
    Guid UserId,
    Guid? AccountId,
    Guid? CardId,
    decimal Amount,
    DateOnly OccurredOn,
    string Description);

[RequiresPermission(FinancesPermissions.Pending.Create)]
public sealed class CreatePendingExpenseCommand(CreatePendingExpenseInput input)
    : CommandBase<CreatePendingExpenseInput, PendingTransactionDto>(input);
