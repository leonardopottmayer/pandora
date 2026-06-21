namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateRecurringTransactionRequest(
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
    bool AutoGenerate = true);
