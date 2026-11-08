using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class LinkErrors
{
    public static Error InvalidUrl =>
        Error.Validation("Links.InvalidUrl", "The link must be an http or https address.");

    /// <summary>The page did not answer, is not a web page, or lives on a private network.</summary>
    public static Error Unreachable =>
        Error.NotFound("Links.Unreachable", "The page could not be read.");
}
