namespace Pottmayer.Pandora.Modules.Notes.Abstractions.Import;

/// <summary>Plain text and its source within Pandora; the caller supplies the authenticated owner.</summary>
public sealed record ImportNoteRequest(Guid UserId, string Title, string Text, string SourcePath, string SourceTitle,
    Guid? SpaceId = null, Guid? FolderId = null);
