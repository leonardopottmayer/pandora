using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.PauseRecurringTransaction;

public sealed record PauseRecurringTransactionInput(Guid UserId, Guid Id);

/// <summary>Pauses generation for a recurring template. Fails if it's already paused or finished.</summary>
[RequiresPermission(FinancesPermissions.Recurrences.Update)]
public sealed class PauseRecurringTransactionCommand(PauseRecurringTransactionInput input)
    : CommandBase<PauseRecurringTransactionInput, RecurringTransactionDto>(input);
