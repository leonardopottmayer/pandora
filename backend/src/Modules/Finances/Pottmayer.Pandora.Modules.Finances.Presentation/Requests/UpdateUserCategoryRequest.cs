namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record UpdateUserCategoryRequest(
    string Name,
    string? Color,
    string? Icon,
    int DisplayOrder);
