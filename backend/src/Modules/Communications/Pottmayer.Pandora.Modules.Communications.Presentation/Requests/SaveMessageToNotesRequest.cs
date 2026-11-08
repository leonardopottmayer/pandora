namespace Pottmayer.Pandora.Modules.Communications.Presentation.Requests;

public sealed record SaveMessageToNotesRequest(string? Title = null, Guid? SpaceId = null, Guid? FolderId = null);
