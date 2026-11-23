namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record UpdateCardRequest(
    string Name,
    string? Brand,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    Guid AccountId);
