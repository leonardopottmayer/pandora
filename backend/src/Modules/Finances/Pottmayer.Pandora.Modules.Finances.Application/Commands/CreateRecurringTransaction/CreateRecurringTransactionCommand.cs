using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateRecurringTransaction;

public sealed record CreateRecurringTransactionInput(
    Guid UserId,
    string Name,
    Guid? AccountId,
    Guid? CardId,
    string Kind,
    decimal? Amount,
    bool AmountIsEstimate,
    string Description,
    string? Payee,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    string Frequency,
    short Interval,
    short? DayOfMonth,
    short? Weekday,
    DateOnly StartDate,
    DateOnly? EndDate,
    int? MaxOccurrences,
    bool AutoPost,
    bool AutoGenerate);

/// <summary>
/// Sets up a new recurring template (e.g. rent, a subscription), active from its start date.
/// The destination, frequency and schedule anchors are fixed at creation — only the cosmetic and
/// future-facing fields can be edited later through <c>UpdateRecurringTransaction</c>.
/// </summary>
public sealed class CreateRecurringTransactionCommand(CreateRecurringTransactionInput input)
    : CommandBase<CreateRecurringTransactionInput, RecurringTransactionDto>(input);
