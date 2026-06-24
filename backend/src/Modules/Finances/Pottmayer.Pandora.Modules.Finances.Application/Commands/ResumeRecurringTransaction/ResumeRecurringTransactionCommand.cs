using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ResumeRecurringTransaction;

public sealed record ResumeRecurringTransactionInput(Guid UserId, Guid Id);

/// <summary>Resumes generation for a paused recurring template. Fails if it's not currently paused.</summary>
public sealed class ResumeRecurringTransactionCommand(ResumeRecurringTransactionInput input)
    : CommandBase<ResumeRecurringTransactionInput, RecurringTransactionDto>(input);
