using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteRecurringTransaction;

public sealed record DeleteRecurringTransactionInput(Guid UserId, Guid Id);

public sealed class DeleteRecurringTransactionCommand(DeleteRecurringTransactionInput input)
    : CommandBase<DeleteRecurringTransactionInput, bool>(input);
