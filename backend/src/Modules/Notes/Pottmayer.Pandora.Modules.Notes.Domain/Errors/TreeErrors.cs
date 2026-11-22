using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

/// <summary>Invalid tree move requests.</summary>
public static class TreeErrors
{
    public static Error InvalidMove => Error.Validation("Tree.InvalidMove", "Choose distinct pages or folders and one destination in the same space.");
    public static Error MoveDenied => Error.Forbidden("Tree.MoveDenied", "You do not have permission to move these items.");
}
