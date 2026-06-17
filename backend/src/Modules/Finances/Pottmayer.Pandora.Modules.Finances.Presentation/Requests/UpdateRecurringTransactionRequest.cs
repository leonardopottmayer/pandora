namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record UpdateRecurringTransactionRequest(
    string Name,
    decimal? Amount,
    bool AmountIsEstimate,
    string Description,
    string? Payee,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    DateOnly? EndDate,
    int? MaxOccurrences,
    bool AutoPost);
