namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record GenerateRecurringTransactionOccurrenceRequest(
    string Destination,
    DateOnly? OccurredOn,
    decimal? Amount,
    string? Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    bool AdvanceSchedule = true);
