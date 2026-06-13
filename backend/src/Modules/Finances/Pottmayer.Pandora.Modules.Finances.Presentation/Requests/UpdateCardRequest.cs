namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record UpdateCardRequest(
    string Name,
    string? Brand,
    string? LastFour,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    Guid? DefaultPaymentAccountId);
