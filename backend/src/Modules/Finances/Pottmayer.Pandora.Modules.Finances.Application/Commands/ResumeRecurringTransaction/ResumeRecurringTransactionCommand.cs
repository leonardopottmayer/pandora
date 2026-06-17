using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ResumeRecurringTransaction;

public sealed record ResumeRecurringTransactionInput(Guid UserId, Guid Id);

public sealed class ResumeRecurringTransactionCommand(ResumeRecurringTransactionInput input)
    : CommandBase<ResumeRecurringTransactionInput, RecurringTransactionDto>(input);
