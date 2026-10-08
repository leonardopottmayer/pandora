using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pottmayer.Pandora.Desktop.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;

namespace Pottmayer.Pandora.Desktop.Files;

/// <summary>
/// The Files agent (product-plan F1): scans the roots the user gave this PC and reports them to the
/// catalog, on each root's schedule or on "Scan now". Read-only — it never writes to a disk.
/// </summary>
/// <remarks>
/// Bridge: <c>files.status</c>, <c>files.scanNow</c>, <c>files.pickFolder</c>, <c>files.listFolders</c>,
/// <c>files.reveal</c>; event <c>files.scanProgress</c>.
/// </remarks>
public sealed class FilesModule : IDesktopModule
{
    public const string ModuleName = "files";

    public string Name => ModuleName;

    public void Register(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<FilesApi>();
        services.AddSingleton<Scanner>();
        services.AddSingleton<FilesAgent>();
        services.AddHostedService(sp => sp.GetRequiredService<FilesAgent>());

        Add(services, "files.status", (sp, _, ct) => sp.GetRequiredService<FilesAgent>().RefreshAsync(ct));

        Add(services, "files.scanNow", (sp, args, _) =>
        {
            sp.GetRequiredService<FilesAgent>().RequestScan(Args.Guid(args, "rootId"));
            return Task.FromResult(true);
        });

        Add(services, "files.pickFolder", async (sp, _, ct) =>
            new { path = await sp.GetRequiredService<IDesktopShell>().PickFolderAsync(ct) });

        Add(services, "files.listFolders", (sp, args, _) =>
            Task.FromResult(Args.OptionalGuid(args, "rootId") is { } rootId
                ? FolderListing.ListInRoot(RootOf(sp, rootId), Args.OptionalString(args, "path") ?? CatalogPath.Root)
                : FolderListing.List(Args.OptionalString(args, "path"))));

        Add(services, "files.reveal", (sp, args, _) =>
        {
            var full = FolderListing.Resolve(RootOf(sp, Args.Guid(args, "rootId")), Args.OptionalString(args, "path") ?? CatalogPath.Root);
            if (!File.Exists(full) && !Directory.Exists(full)) throw new InvalidOperationException("Not found on this device.");
            sp.GetRequiredService<IDesktopShell>().Reveal(full);
            return Task.FromResult(true);
        });
    }

    private static RootConfig RootOf(IServiceProvider sp, Guid rootId) =>
        sp.GetRequiredService<FilesAgent>().FindRoot(rootId)
        ?? throw new InvalidOperationException("That root is not on this device.");

    private static void Add<T>(IServiceCollection services, string method, Func<IServiceProvider, JsonElement?, CancellationToken, Task<T>> handle) =>
        services.AddSingleton<IBridgeHandler>(sp => new BridgeMethod(method, async (args, ct) => await handle(sp, args, ct)));
}

/// <summary>Folders for the page's pickers and selection tree. Never lists files: the catalog does that.</summary>
internal static class FolderListing
{
    /// <param name="CatalogPath">Set when listing inside a root: the path a selection mark uses.</param>
    public sealed record Folder(string Name, string Path, string? CatalogPath = null);

    private static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
    };

    /// <summary>The subfolders of an absolute path, or the ready drives when there is none.</summary>
    public static IReadOnlyList<Folder> List(string? path) => string.IsNullOrWhiteSpace(path)
        ? [.. DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => new Folder(Label(d), d.RootDirectory.FullName))]
        : [.. Subfolders(path).Select(d => new Folder(d.Name, d.FullName))];

    /// <summary>The subfolders of a folder of a root, with their catalog paths.</summary>
    public static IReadOnlyList<Folder> ListInRoot(RootConfig root, string catalogPath) =>
        [.. Subfolders(Resolve(root, catalogPath)).Select(d =>
        {
            var name = d.Name.Normalize(NormalizationForm.FormC);
            return new Folder(d.Name, d.FullName, catalogPath == CatalogPath.Root ? CatalogPath.Root + name : catalogPath + "/" + name);
        })];

    /// <summary>Where a catalog path of the root is on this disk.</summary>
    public static string Resolve(RootConfig root, string catalogPath)
    {
        if (!CatalogPath.IsValid(catalogPath)) throw new ArgumentException($"'{catalogPath}' is not a catalog path.");
        return Path.Combine(root.LocalPath, catalogPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    private static IEnumerable<DirectoryInfo> Subfolders(string path) =>
        new DirectoryInfo(path).EnumerateDirectories("*", Options).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase);

    private static string Label(DriveInfo drive)
    {
        try
        {
            return string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\', '/')})";
        }
        catch (IOException)
        {
            return drive.Name;
        }
    }
}

internal static class Args
{
    public static Guid Guid(JsonElement? args, string name) =>
        OptionalGuid(args, name) ?? throw new ArgumentException($"{name} is required.");

    public static Guid? OptionalGuid(JsonElement? args, string name) =>
        Property(args, name) is { ValueKind: JsonValueKind.String } v && v.TryGetGuid(out var id) ? id : null;

    public static string? OptionalString(JsonElement? args, string name) =>
        Property(args, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static JsonElement? Property(JsonElement? args, string name) =>
        args is { ValueKind: JsonValueKind.Object } a && a.TryGetProperty(name, out var v) ? v : null;
}
