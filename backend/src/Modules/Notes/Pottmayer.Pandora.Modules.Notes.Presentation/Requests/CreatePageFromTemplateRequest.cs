namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record CreatePageFromTemplateRequest(Guid TemplateId, string Title, Guid? SpaceId = null, Guid? FolderId = null);
