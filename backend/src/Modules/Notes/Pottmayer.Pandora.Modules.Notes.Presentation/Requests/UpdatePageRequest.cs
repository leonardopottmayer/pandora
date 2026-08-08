namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record UpdatePageRequest(
    string Title,
    string? Icon,
    string ContentMarkdown);
