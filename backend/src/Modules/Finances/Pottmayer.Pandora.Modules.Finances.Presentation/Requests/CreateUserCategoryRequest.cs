namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateUserCategoryRequest(
    string Name,
    string Nature,
    Guid? ParentCategoryId,
    string? Color,
    string? Icon,
    int DisplayOrder);
