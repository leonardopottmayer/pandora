namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>What produced an immutable page version.</summary>
public static class PageVersionSource
{
    public const string Create = "create";
    public const string Initial = "initial";
    public const string Edit = "edit";
    public const string Assistant = "assistant";
    public const string Restore = "restore";
}
