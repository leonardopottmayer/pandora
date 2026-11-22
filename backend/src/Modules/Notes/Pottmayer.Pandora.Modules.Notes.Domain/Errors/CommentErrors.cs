using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class CommentErrors
{
    public static Error NotFound =>
        Error.NotFound("Comments.NotFound", "Comment not found.");

    public static Error InvalidBody =>
        Error.Validation("Comments.InvalidBody", "A comment needs text, up to 5000 characters.");

    public static Error InvalidAnchor =>
        Error.Validation("Comments.InvalidAnchor", "A comment needs the block it is on, and a quote of up to 500 characters.");

    /// <summary>Only a thread's first comment is resolved, and a reply is never answered on its own.</summary>
    public static Error NotAThread =>
        Error.Validation("Comments.NotAThread", "This is a reply, not a thread.");
}
