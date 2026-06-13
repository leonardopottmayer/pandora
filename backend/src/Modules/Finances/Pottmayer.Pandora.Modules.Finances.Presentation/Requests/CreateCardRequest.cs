namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateCardRequest(
    string Name,
    string? Brand,
    string? LastFour,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    string Currency,
    Guid? DefaultPaymentAccountId);
