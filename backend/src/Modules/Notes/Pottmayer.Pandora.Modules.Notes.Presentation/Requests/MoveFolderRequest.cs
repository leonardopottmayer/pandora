namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record MoveFolderRequest(Guid? ParentId, int OrderIndex);
