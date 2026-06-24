using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.GenerateRecurringTransactionOccurrence;

/// <summary>
/// Materializes one occurrence of a recurring transaction on demand. The schedule is advanced as if
/// the job had generated it (cursor moves from <c>NextOccurrenceOn</c> using the recurrence rule),
/// while the materialized record may carry user overrides for date, description, amount, etc.
/// </summary>
public sealed record GenerateRecurringTransactionOccurrenceInput(
    Guid UserId,
    Guid RecurringTransactionId,
    string Destination,
    bool AdvanceSchedule,
    DateOnly? OccurredOn,
    decimal? Amount,
    string? Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId);

/// <summary>Manually triggers one occurrence of a recurring template ahead of the scheduled job.</summary>
public sealed class GenerateRecurringTransactionOccurrenceCommand(GenerateRecurringTransactionOccurrenceInput input)
    : CommandBase<GenerateRecurringTransactionOccurrenceInput, GeneratedOccurrenceDto>(input);
