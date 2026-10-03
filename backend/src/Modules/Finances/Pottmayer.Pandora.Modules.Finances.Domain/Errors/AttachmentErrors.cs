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

    /// <summary>Neither or both of transaction and pending transaction were given.</summary>
    public static Error OwnerRequired =>
        Error.Validation("Attachments.OwnerRequired", "Attach the file to either a transaction or a pending transaction.");

    /// <summary>The owner does not exist or is not the user's (404-on-foreign-resource rule).</summary>
    public static Error OwnerNotFound =>
        Error.NotFound("Attachments.OwnerNotFound", "The transaction to attach to does not exist.");
}
