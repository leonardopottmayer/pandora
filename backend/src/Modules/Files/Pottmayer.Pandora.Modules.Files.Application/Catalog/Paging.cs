using Pottmayer.Pandora.Modules.Files.Application.Dtos;

namespace Pottmayer.Pandora.Modules.Files.Application.Catalog;

/// <summary>Offset paging that reads one row more than asked, to know whether there is a next page.</summary>
internal static class Paging
{
    public const int MaxTake = 200;

    public static (int Skip, int Take) Clamp(int skip, int take) =>
        (Math.Max(0, skip), take is > 0 and <= MaxTake ? take : 50);

    public static PageDto<TOut> Page<TIn, TOut>(IReadOnlyList<TIn> rows, int take, Func<TIn, TOut> map) =>
        new([.. rows.Take(take).Select(map)], rows.Count > take);
}
