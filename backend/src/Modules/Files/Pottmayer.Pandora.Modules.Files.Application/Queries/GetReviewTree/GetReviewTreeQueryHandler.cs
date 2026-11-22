using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetReviewTree;

/// <summary>
/// The review inbox as a tree, one level at a time: a whole subtree gone reads as one folder with a count,
/// and folders load on expand, so a root with millions of entries never loads at once.
/// </summary>
public sealed class GetReviewTreeQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetReviewTreeQuery, IReadOnlyList<ReviewNodeDto>>
{
    // ponytail: one flat folder with more children than this shows only the first ones; page it if that happens.
    private const int MaxChildren = 1000;

    protected override async Task<Result<IReadOnlyList<ReviewNodeDto>>> HandleAsync(GetReviewTreeQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var parentPath = CatalogPath.Root;
        if (input.ParentPath is not null && !RootRules.TryNormalize(input.ParentPath, out parentPath))
            return Fail(FilesErrors.InvalidPath(input.ParentPath));

        return await factory.ExecuteAsync<Result<IReadOnlyList<ReviewNodeDto>>>(FilesModule.DatabaseKey, async (ctx, ct) =>
        {
            var roots = ctx.AcquireRepository<IRootRepository>();
            var entries = ctx.AcquireRepository<IEntryRepository>();

            if (input.RootId is not { } rootId)
            {
                // Removed roots count too: their entries wait here as excluded.
                var counts = await entries.CountReviewByRootAsync(input.UserId, ct);
                var nodes = new List<ReviewNodeDto>();
                foreach (var (id, count) in counts)
                {
                    var r = await roots.FindForUserAsync(id, input.UserId, ct);
                    if (r is not null)
                        nodes.Add(new ReviewNodeDto(r.Id, r.Name, CatalogPath.Root, count, true, null, null, null));
                }
                return Result<IReadOnlyList<ReviewNodeDto>>.Success([.. nodes.OrderBy(n => n.Name)]);
            }

            var root = await roots.FindForUserAsync(rootId, input.UserId, ct);
            if (root is null) return FilesErrors.RootNotFound;

            var children = await entries.ListReviewChildrenAsync(root.Id, parentPath, MaxChildren, ct);
            return Result<IReadOnlyList<ReviewNodeDto>>.Success([.. children.Select(n => ReviewNodeDto.From(root.Id, n))]);
        }, cancellationToken: cancellationToken);
    }
}
