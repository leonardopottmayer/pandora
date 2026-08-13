using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class AttachmentErrors
{
    public static Error NotFound =>
        Error.NotFound("Attachments.NotFound", "Attachment not found.");

    public static Error Empty =>
        Error.Validation("Attachments.Empty", "The uploaded file is empty.");

    public static Error TooLarge =>
        Error.Validation("Attachments.TooLarge", "The uploaded file exceeds the maximum allowed size.");

    /// <summary>The page an attachment is being pinned to does not exist or is not owned by the user.</summary>
    public static Error PageNotFound =>
        Error.NotFound("Attachments.PageNotFound", "The page does not exist.");
}
