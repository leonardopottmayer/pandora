using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Files.Application.Catalog;

/// <summary>A root's configuration as its agent receives it, and the same rules for the backend's own checks.</summary>
internal static class RootRules
{
    public static async Task<RootConfig> LoadConfigAsync(IDataContext ctx, Root root, CancellationToken ct)
    {
        var filters = await ctx.AcquireRepository<IFilterRepository>()
                               .ListEnabledForRootAsync(root.UserId, root.DeviceId, root.Id, ct);

        return new RootConfig(
            root.Id, root.Name, root.LocalPath, root.CaseSensitive, root.IncludeHidden, root.ScanTime,
            [.. root.Marks.Select(m => new MarkConfig(m.Path, m.Mode.Value))],
            [.. filters.Select(ToConfig)]);
    }

    public static async Task<CatalogRules> LoadAsync(IDataContext ctx, Root root, CancellationToken ct)
    {
        var config = await LoadConfigAsync(ctx, root, ct);
        return new CatalogRules(config.CaseSensitive, config.Marks, config.Filters);
    }

    public static FilterConfig ToConfig(Filter f) =>
        new(f.Action.Value, f.AppliesTo.Value, f.Matcher.Value, f.Pattern, f.CaseSensitive, f.ScopePath);

    /// <summary>Normalizes a path the user or an agent typed; false when it is not a valid catalog path.</summary>
    public static bool TryNormalize(string? input, out string path)
    {
        path = input is null ? string.Empty : CatalogPath.Normalize(input);
        return input is not null && CatalogPath.IsValid(path);
    }
}
