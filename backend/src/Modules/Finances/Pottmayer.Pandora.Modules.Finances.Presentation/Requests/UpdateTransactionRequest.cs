namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record UpdateTransactionRequest(
    string Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId);
