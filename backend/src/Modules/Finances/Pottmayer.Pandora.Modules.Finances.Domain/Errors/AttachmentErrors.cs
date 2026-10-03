using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class AttachmentErrors
{
    public static Error NotFound =>
        Error.NotFound("Attachments.NotFound", "Attachment not found.");

    public static Error Empty =>
        Error.Validation("Attachments.Empty", "The uploaded file is empty.");

    public static Error TooLarge =>
        Error.Validation("Attachments.TooLarge", "The uploaded file exceeds the maximum allowed size.");

    public static Error UnsupportedType =>
        Error.Validation("Attachments.UnsupportedType", "Only images and PDFs can be attached.");

    public static Error InvalidKind(string kind) =>
        Error.Validation("Attachments.InvalidKind", $"Attachment kind '{kind}' is not supported.");

    /// <summary>More than one owner was given (or none, where one is needed).</summary>
    public static Error OwnerRequired =>
        Error.Validation("Attachments.OwnerRequired", "Attach the file to one transaction, pending transaction or statement.");

    /// <summary>Only a queued attachment (no owner yet) can be assigned.</summary>
    public static Error AlreadyAssigned =>
        Error.Conflict("Attachments.AlreadyAssigned", "The file is already attached to something.");

    /// <summary>The owner does not exist or is not the user's (404-on-foreign-resource rule).</summary>
    public static Error OwnerNotFound =>
        Error.NotFound("Attachments.OwnerNotFound", "The transaction to attach to does not exist.");
}
