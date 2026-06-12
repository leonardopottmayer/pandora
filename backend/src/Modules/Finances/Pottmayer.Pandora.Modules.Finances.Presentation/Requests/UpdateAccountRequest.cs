namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record UpdateAccountRequest(
    string Name,
    string Type,
    string? Institution,
    string? Description,
    string? Color,
    string? Icon,
    int DisplayOrder);
