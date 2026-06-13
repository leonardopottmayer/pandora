namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateTransactionRequest(
    Guid? AccountId,
    Guid? CardId,
    Guid? CardStatementId,
    string Kind,
    decimal Amount,
    DateOnly OccurredOn,
    string Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId);
