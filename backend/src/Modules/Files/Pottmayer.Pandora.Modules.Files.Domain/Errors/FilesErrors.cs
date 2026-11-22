using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Files.Domain.Errors;

public static class FilesErrors
{
    public static Error NotEnabled =>
        Error.Forbidden("Files.NotEnabled", "Files is turned off for this account.");

    public static Error DeviceNotFound =>
        Error.NotFound("Files.DeviceNotFound", "That device was not found among your paired devices.");

    public static Error RootNotFound =>
        Error.NotFound("Files.RootNotFound", "That root was not found.");

    public static Error InvalidRootName =>
        Error.Validation("Files.InvalidRootName", "A root needs a name of up to 100 characters.");

    public static Error InvalidLocalPath =>
        Error.Validation("Files.InvalidLocalPath", "A root needs a path on the device, of up to 1024 characters.");

    public static Error RootAlreadyExists =>
        Error.Conflict("Files.RootAlreadyExists", "That path is already a root of this device.");

    public static Error InvalidPath(string path) =>
        Error.Validation("Files.InvalidPath", $"'{path}' is not a valid catalog path.");

    public static Error DuplicateMark(string path) =>
        Error.Validation("Files.DuplicateMark", $"'{path}' is marked more than once.");

    public static Error InvalidSelectionMode =>
        Error.Validation("Files.InvalidSelectionMode", "A mark is either 'include' or 'exclude'.");

    public static Error FilterNotFound =>
        Error.NotFound("Files.FilterNotFound", "That filter was not found.");

    public static Error InvalidFilterName =>
        Error.Validation("Files.InvalidFilterName", "A filter needs a name of up to 100 characters.");

    public static Error InvalidFilterAction =>
        Error.Validation("Files.InvalidFilterAction", "A filter either includes or excludes.");

    public static Error InvalidFilterTarget =>
        Error.Validation("Files.InvalidFilterTarget", "A filter applies to files or to folders.");

    public static Error FolderFilterIncludes =>
        Error.Validation("Files.FolderFilterIncludes", "A folder filter can only exclude.");

    public static Error InvalidPattern(string reason) =>
        Error.Validation("Files.InvalidPattern", $"The pattern cannot be used: {reason}");

    public static Error ScopePathNeedsRoot =>
        Error.Validation("Files.ScopePathNeedsRoot", "A folder scope needs its root.");

    public static Error ScanNotFound =>
        Error.NotFound("Files.ScanNotFound", "That scan was not found.");

    public static Error ScanAlreadyRunning =>
        Error.Conflict("Files.ScanAlreadyRunning", "This root already has a running scan.");

    public static Error ScanNotRunning =>
        Error.Conflict("Files.ScanNotRunning", "That scan is no longer running.");

    public static Error ScanNotHeld =>
        Error.Conflict("Files.ScanNotHeld", "That scan is not waiting for a decision.");

    public static Error BatchTooLarge =>
        Error.Validation("Files.BatchTooLarge", "A batch holds at most 1000 entries.");

    public static Error InvalidEntryKind =>
        Error.Validation("Files.InvalidEntryKind", "An entry is a 'file' or a 'directory'.");

    public static Error InvalidFingerprint =>
        Error.Validation("Files.InvalidFingerprint", "A fingerprint is 64 lower-case hex characters.");

    public static Error EntryNotFound =>
        Error.NotFound("Files.EntryNotFound", "That entry was not found.");
}
