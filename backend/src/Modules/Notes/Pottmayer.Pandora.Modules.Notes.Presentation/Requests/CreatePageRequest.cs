namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record CreatePageRequest(
    string Title,
    Guid? ParentId,
    string? Icon,
    string? ContentMarkdown);
