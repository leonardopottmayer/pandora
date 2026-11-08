namespace Pottmayer.Pandora.Modules.Notes.Presentation.Requests;

/// <summary>Creates (with a parent or a space) or updates (name, icon, description) a folder.</summary>
public sealed record SaveFolderRequest(
    string Name,
    string? Icon,
    string? Description,
    Guid? ParentId = null,
    Guid? SpaceId = null);
