namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record MoveTreeItemsRequest(Guid SpaceId, string Kind, Guid? ParentId, Guid? FolderId, IReadOnlyList<Guid> ItemIds);
