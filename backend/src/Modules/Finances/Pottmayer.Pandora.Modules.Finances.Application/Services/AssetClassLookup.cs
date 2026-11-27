using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>What a class id names, whichever table it lives in.</summary>
internal sealed record AssetClassInfo(AssetClassRef Ref, AssetBehavior Behavior, bool IsArchived);

/// <summary>
/// The API speaks of one asset class id; the classes live in two tables, like the categories: the
/// system's (fin020) and the user's own (fin025). This finds an id in either.
/// </summary>
internal static class AssetClassLookup
{
    public static async Task<AssetClassInfo?> FindAsync(IDataContext ctx, Guid id, Guid userId, CancellationToken ct)
    {
        if (await ctx.AcquireRepository<ISystemAssetClassRepository>().FindAsync(id, ct) is { } system)
            return new(new AssetClassRef(system.Id, IsSystem: true), system.Behavior, IsArchived: false);

        var own = await ctx.AcquireRepository<IUserAssetClassRepository>().FindByIdForUserAsync(id, userId, ct);
        return own is null ? null : new(new AssetClassRef(own.Id, IsSystem: false), own.Behavior, own.IsArchived);
    }

    /// <summary>The system classes in display order, then the user's own by name.</summary>
    public static async Task<IReadOnlyList<AssetClassDto>> ListAsync(IDataContext ctx, Guid userId, CancellationToken ct)
    {
        var system = await ctx.AcquireRepository<ISystemAssetClassRepository>().ListAsync(ct);
        var own = await ctx.AcquireRepository<IUserAssetClassRepository>().ListByUserAsync(userId, ct);
        return [.. system.Select(AssetClassDto.From), .. own.Select(AssetClassDto.From)];
    }
}
