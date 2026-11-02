using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class MediaErrors
{
    public static Error NotFound =>
        Error.NotFound("Media.NotFound", "Media item not found.");

    public static Error Empty =>
        Error.Validation("Media.Empty", "The uploaded file is empty.");

    public static Error TooLarge =>
        Error.Validation("Media.TooLarge", "The uploaded file exceeds the maximum allowed size.");

    /// <summary>A kind the schema knows but the upload does not take yet — videos, for now.</summary>
    public static Error UnsupportedKind =>
        Error.Validation("Media.UnsupportedKind", "This kind of file is not accepted yet.");

    public static Error InvalidName =>
        Error.Validation("Media.InvalidName", "A name is required.");

    public static Error FolderNotFound =>
        Error.NotFound("Media.FolderNotFound", "Folder not found.");

    public static Error FolderCycle =>
        Error.Conflict("Media.FolderCycle", "A folder cannot be moved into itself or one of its subfolders.");
}
