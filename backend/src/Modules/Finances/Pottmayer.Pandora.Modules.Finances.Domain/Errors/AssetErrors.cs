using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class AssetErrors
{
    public static Error NotFound =>
        Error.NotFound("Assets.NotFound", "Asset not found.");

    public static Error InvalidName =>
        Error.Validation("Assets.InvalidName", "The asset needs a name.");

    public static Error NameAlreadyExists =>
        Error.Conflict("Assets.NameAlreadyExists", "An asset with this name already exists.");

    public static Error CodeAlreadyExists =>
        Error.Conflict("Assets.CodeAlreadyExists", "An asset of this class already has this code.");

    public static Error Archived =>
        Error.Conflict("Assets.Archived", "The asset is archived.");

    public static Error ClassNotFound =>
        Error.NotFound("AssetClasses.NotFound", "Asset class not found.");

    public static Error ClassNameAlreadyExists =>
        Error.Conflict("AssetClasses.NameAlreadyExists", "A class with this name already exists.");

    public static Error ClassReadOnly =>
        Error.Conflict("AssetClasses.ReadOnly", "System classes cannot be changed.");

    public static Error ClassArchived =>
        Error.Conflict("AssetClasses.Archived", "The asset class is archived.");

    public static Error ClassInUse =>
        Error.Conflict("AssetClasses.InUse", "The asset class has assets; archive it instead.");

    public static Error ClassBehaviorMismatch =>
        Error.Validation("Assets.ClassBehaviorMismatch", "An asset can only move to a class with the same behavior.");

    public static Error InvalidBehavior(string behavior) =>
        Error.Validation("AssetClasses.InvalidBehavior", $"Asset behavior '{behavior}' is not supported.");
}
