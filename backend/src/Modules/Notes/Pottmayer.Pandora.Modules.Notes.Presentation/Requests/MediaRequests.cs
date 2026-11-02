namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

public sealed record RenameMediaRequest(string Name);

/// <summary>A folder, or null to take the item out of every folder ("Unsorted").</summary>
public sealed record MoveMediaRequest(Guid? FolderId);

public sealed record CreateMediaFolderRequest(string Name, Guid? ParentId);

public sealed record RenameMediaFolderRequest(string Name);

/// <summary>A folder to go under, or null for the top.</summary>
public sealed record MoveMediaFolderRequest(Guid? ParentId);
