namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record MovePageRequest(
    Guid? ParentId,
    int OrderIndex,
    Guid? SpaceId = null,
    Guid? FolderId = null);
