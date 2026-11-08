using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class FolderErrors
{
    public static Error NotFound =>
        Error.NotFound("Folders.NotFound", "Folder not found.");

    public static Error InvalidName =>
        Error.Validation("Folders.InvalidName", "Folder name is required.");

    /// <summary>The parent folder is not the user's, or lives in another space.</summary>
    public static Error ParentNotFound =>
        Error.NotFound("Folders.ParentNotFound", "The parent folder does not exist in this space.");

    public static Error CycleDetected =>
        Error.Conflict("Folders.CycleDetected", "A folder cannot be moved into itself or one of its subfolders.");
}
