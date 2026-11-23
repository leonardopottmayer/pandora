using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>Builds <see cref="PositionDto"/>s with their assets and latest values in two queries.</summary>
internal static class PositionReader
{
    public static async Task<IReadOnlyList<PositionDto>> ReadAsync(
        IDataContext ctx, Guid userId, IReadOnlyList<Position> positions, CancellationToken ct)
    {
        if (positions.Count == 0) return [];

        var assets = (await ctx.AcquireRepository<IAssetRepository>()
            .GetByIdsAsync([.. positions.Select(p => p.AssetId).Distinct()], userId, ct)).ToDictionary(a => a.Id);
        var latest = await ctx.AcquireRepository<IPositionValuationRepository>()
            .GetLatestAsync([.. positions.Select(p => p.Id)], ct);

        return [.. positions.Select(p => PositionDto.From(p, assets[p.AssetId], latest.GetValueOrDefault(p.Id)))];
    }

    public static async Task<PositionDto> ReadAsync(IDataContext ctx, Guid userId, Position position, CancellationToken ct)
        => (await ReadAsync(ctx, userId, [position], ct))[0];

    /// <summary>The value a partial redemption takes its share from: the latest reported value, else the cost.</summary>
    public static async Task<decimal> CurrentValueAsync(IDataContext ctx, Position position, CancellationToken ct)
    {
        var latest = await ctx.AcquireRepository<IPositionValuationRepository>().GetLatestAsync([position.Id], ct);
        return latest.GetValueOrDefault(position.Id)?.Value ?? position.CostBasis;
    }
}
