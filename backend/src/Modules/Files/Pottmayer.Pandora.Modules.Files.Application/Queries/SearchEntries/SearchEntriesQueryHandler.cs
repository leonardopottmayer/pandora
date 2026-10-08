using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ReadModels;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.SearchEntries;

/// <summary>Finds entries by fragments of their name (<c>breaking bad s02</c>) across every root of the user.</summary>
public sealed class SearchEntriesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<SearchEntriesQuery, PageDto<EntryDto>>
{
    public const string AllStatuses = "all";

    protected override async Task<Result<PageDto<EntryDto>>> HandleAsync(SearchEntriesQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var (skip, take) = Paging.Clamp(input.Skip, input.Take);
        var terms = (input.Query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Unknown values filter nothing out rather than failing the search.
        var category = FileCategory.IsSupported(input.Category) ? FileCategory.FromValue(input.Category!) : null;
        var status = input.Status == AllStatuses ? null
            : EntryStatus.IsSupported(input.Status) ? EntryStatus.FromValue(input.Status!) : EntryStatus.Present;

        var entries = await factory.ExecuteAsync(FilesModule.DatabaseKey, async (ctx, ct) =>
        {
            IReadOnlyCollection<Guid>? rootIds = null;
            if (input.DeviceId is not null || input.RootId is not null)
            {
                var roots = await ctx.AcquireRepository<IRootRepository>().ListActiveByUserAsync(input.UserId, ct);
                rootIds = [.. roots.Where(r => (input.DeviceId is null || r.DeviceId == input.DeviceId)
                                               && (input.RootId is null || r.Id == input.RootId))
                                   .Select(r => r.Id)];
            }

            var search = new EntrySearch(input.UserId, terms, rootIds, category, input.MinSize, input.MaxSize,
                input.ModifiedFrom?.ToUniversalTime(), input.ModifiedTo?.ToUniversalTime(), status);
            return await ctx.AcquireRepository<IEntryRepository>().SearchAsync(search, skip, take + 1, ct);
        }, cancellationToken: cancellationToken);

        return Ok(Paging.Page(entries, take, EntryDto.From));
    }
}
