using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.PreviewFilter;

/// <summary>
/// What a draft filter matches in the catalog now — a count and a sample — so a pattern can be tried
/// without waiting for a scan. Tests the filter alone, against the entries of the roots in its scope.
/// </summary>
public sealed class PreviewFilterQueryHandler(IUnitOfWorkFactory factory, IDeviceReader devices)
    : QueryHandlerBase<PreviewFilterQuery, FilterPreviewDto>
{
    private const int SampleSize = 20;

    protected override async Task<Result<FilterPreviewDto>> HandleAsync(PreviewFilterQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<FilterPreviewDto>>(FilesModule.DatabaseKey, async (ctx, ct) =>
        {
            var validated = await FilterInputs.ValidateAsync(input.Filter, input.UserId, ctx, devices, ct);
            if (validated.IsFailure) return Result<FilterPreviewDto>.Failure(validated.Errors);
            var filter = validated.Value!;

            var roots = (await ctx.AcquireRepository<IRootRepository>().ListActiveByUserAsync(input.UserId, ct))
                .Where(r => (filter.RootId is null || r.Id == filter.RootId) && (filter.DeviceId is null || r.DeviceId == filter.DeviceId))
                .ToDictionary(r => r.Id);

            // One matcher per case setting in play: the filter's own, or each root's.
            var matchers = roots.Values.Select(r => filter.CaseSensitive ?? r.CaseSensitive).Distinct()
                .ToDictionary(cs => cs, cs => NameMatcher.Create(filter.Matcher.Value, filter.Pattern, cs));
            var kind = filter.AppliesTo == FilterTarget.Folder ? EntryKind.Directory : EntryKind.File;

            var count = 0;
            var sample = new List<FilterPreviewItem>();
            // ponytail: walks every entry of the roots in scope; a server-side regex index if previews get slow.
            await foreach (var item in ctx.AcquireRepository<IEntryRepository>().StreamAsync(roots.Keys, ct))
            {
                if (item.Kind != kind) continue;
                var root = roots[item.RootId];
                var comparison = root.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (filter.ScopePath is { } scope && !CatalogPath.IsUnder(item.RelativePath, scope, comparison)) continue;
                if (!matchers[filter.CaseSensitive ?? root.CaseSensitive](CatalogPath.Name(item.RelativePath), item.RelativePath)) continue;

                count++;
                if (sample.Count < SampleSize) sample.Add(new FilterPreviewItem(item.Id, item.RootId, item.RelativePath));
            }

            return Result<FilterPreviewDto>.Success(new FilterPreviewDto(count, sample));
        }, cancellationToken: cancellationToken);
    }
}
