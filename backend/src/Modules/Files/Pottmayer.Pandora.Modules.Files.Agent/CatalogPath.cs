using System.Text;

namespace Pottmayer.Pandora.Modules.Files.Agent;

/// <summary>
/// A path inside a root, the same on every platform: <c>/</c> is the root, segments are separated by
/// <c>/</c> and in Unicode NFC (macOS hands out NFD; without normalizing, <c>Ação</c> from a Mac and from
/// Windows would be two different names).
/// </summary>
public static class CatalogPath
{
    public const string Root = "/";

    /// <summary>Longer paths are not cataloged; an agent skips them instead of sending them.</summary>
    public const int MaxLength = 1024;

    /// <summary>Turns a path relative to the root, with either separator, into the catalog form.</summary>
    public static string Normalize(string relativePath) =>
        Root + relativePath.Replace('\\', '/').Normalize(NormalizationForm.FormC).Trim('/');

    public static bool IsValid(string? path) =>
        path is { Length: > 0 and <= MaxLength }
        && path[0] == '/'
        && !path.Contains('\\') && !path.Contains('\0')
        && path.IsNormalized(NormalizationForm.FormC)
        && (path == Root || path[1..].Split('/').All(s => s is not ("" or "." or "..")));

    /// <summary>The folder holding <paramref name="path"/>; the root is its own parent.</summary>
    public static string Parent(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash <= 0 ? Root : path[..slash];
    }

    public static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];

    /// <summary>Lower-case, without the dot; null for <c>README</c> or <c>.gitignore</c>.</summary>
    public static string? Extension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 ? name[(dot + 1)..].ToLowerInvariant() : null;
    }

    /// <summary>What every path strictly below <paramref name="folder"/> starts with.</summary>
    public static string ChildPrefix(string folder) => folder == Root ? Root : folder + "/";

    /// <summary>Whether <paramref name="path"/> lies strictly below <paramref name="folder"/>.</summary>
    public static bool IsUnder(string path, string folder, StringComparison comparison = StringComparison.Ordinal) =>
        path.Length > folder.Length && path.StartsWith(ChildPrefix(folder), comparison);
}
