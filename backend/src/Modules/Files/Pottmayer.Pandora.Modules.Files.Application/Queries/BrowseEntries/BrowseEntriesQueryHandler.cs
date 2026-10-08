using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.BrowseEntries;

/// <summary>The children of one folder of a root, folders first. Works for removed roots too, to look back.</summary>
public sealed class BrowseEntriesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<BrowseEntriesQuery, PageDto<EntryDto>>
{
    protected override async Task<Result<PageDto<EntryDto>>> HandleAsync(BrowseEntriesQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var parentPath = CatalogPath.Root;
        if (input.ParentPath is not null && !RootRules.TryNormalize(input.ParentPath, out parentPath))
            return Fail(FilesErrors.InvalidPath(input.ParentPath));

        var (skip, take) = Paging.Clamp(input.Skip, input.Take);

        return await factory.ExecuteAsync<Result<PageDto<EntryDto>>>(FilesModule.DatabaseKey, async (ctx, ct) =>
        {
            var root = await ctx.AcquireRepository<IRootRepository>().FindForUserAsync(input.RootId, input.UserId, ct);
            if (root is null) return FilesErrors.RootNotFound;

            var entries = await ctx.AcquireRepository<IEntryRepository>().BrowseAsync(root.Id, parentPath, skip, take + 1, ct);
            return Result<PageDto<EntryDto>>.Success(Paging.Page(entries, take, EntryDto.From));
        }, cancellationToken: cancellationToken);
    }
}
