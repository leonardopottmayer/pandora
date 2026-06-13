namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateAccountRequest(
    string Name,
    string Type,
    string Currency,
    string? Institution,
    string? Description,
    string? Color,
    string? Icon,
    int DisplayOrder,
    decimal? OpeningBalance);
