using System.Security;
using System.Text;
using Pottmayer.Pandora.Modules.Files.Agent;

namespace Pottmayer.Pandora.Desktop.Files;

/// <summary>A file or folder the walk lets through: its catalog path, where it is on disk, and its stat.</summary>
internal sealed record WalkItem(string Path, string FullPath, bool IsDirectory, long Size, DateTimeOffset ModifiedAt)
{
    public ScannedEntry ToEntry(string? fingerprint, FileMetadata? metadata = null) => IsDirectory
        ? new ScannedEntry(Path, AgentValues.Directory, 0, null, null)
        : new ScannedEntry(Path, AgentValues.File, Size, ModifiedAt, fingerprint, metadata);
}

/// <summary>
/// Walks a root, read-only, streaming what the selection and filters let through. Excluded folders are
/// pruned — never entered — so an excluded branch costs nothing. Memory stays flat on millions of files:
/// only the folders still to visit are held.
/// </summary>
internal static class Walker
{
    public static IEnumerable<WalkItem> Walk(string localPath, CatalogRules rules, bool includeHidden, CancellationToken ct)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            // Hidden and system items ($RECYCLE.BIN, System Volume Information, …) are skipped unless the root asks for them.
            AttributesToSkip = includeHidden ? 0 : FileAttributes.Hidden | FileAttributes.System,
        };

        var pending = new Stack<(string FullPath, string Path)>();
        pending.Push((localPath, CatalogPath.Root));

        while (pending.TryPop(out var folder))
        {
            ct.ThrowIfCancellationRequested();

            List<FileSystemInfo> children;
            try
            {
                children = [.. new DirectoryInfo(folder.FullPath).EnumerateFileSystemInfos("*", options)];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                continue; // an unreadable folder is skipped, not fatal
            }

            foreach (var child in children)
            {
                var name = child.Name.Normalize(NormalizationForm.FormC);
                var path = folder.Path == CatalogPath.Root ? CatalogPath.Root + name : folder.Path + "/" + name;
                if (!CatalogPath.IsValid(path)) continue; // too long, or a name a catalog path cannot hold

                if (child is DirectoryInfo directory)
                {
                    // Junctions and symlinks may loop; what they point to is cataloged where it lives.
                    if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint) || !rules.IncludesFolder(path)) continue;

                    yield return new WalkItem(path, directory.FullName, true, 0, directory.LastWriteTimeUtc);
                    pending.Push((directory.FullName, path));
                }
                else if (child is FileInfo file && rules.IncludesFile(path))
                {
                    yield return new WalkItem(path, file.FullName, false, file.Length, file.LastWriteTimeUtc);
                }
            }
        }
    }
}
