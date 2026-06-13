namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateTransactionRequest(
    Guid AccountId,
    string Kind,
    decimal Amount,
    DateOnly OccurredOn,
    string Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId);
