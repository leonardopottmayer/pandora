using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteRecurringTransaction;

public sealed record DeleteRecurringTransactionInput(Guid UserId, Guid Id);

/// <summary>
/// Permanently removes a recurring template. Past transactions and pending suggestions it already
/// generated are not touched — only the template itself is deleted.
/// </summary>
public sealed class DeleteRecurringTransactionCommand(DeleteRecurringTransactionInput input)
    : CommandBase<DeleteRecurringTransactionInput, bool>(input);
