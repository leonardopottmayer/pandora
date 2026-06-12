using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetSystemCategories;

public sealed class GetSystemCategoriesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetSystemCategoriesQuery, IReadOnlyList<SystemCategoryDto>>
{
    protected override async Task<Result<IReadOnlyList<SystemCategoryDto>>> HandleAsync(
        GetSystemCategoriesQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var all = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var reader = ctx.AcquireRepository<ISystemCategoryReader>();
            return await reader.GetAllAsync(input.Nature, input.IncludeInactive, token);
        }, cancellationToken: ct);

        var childrenByParent = all
            .Where(c => c.ParentCategoryId is not null)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SystemCategory>)[.. g]);

        IReadOnlyList<SystemCategoryDto> tree =
        [
            .. all
                .Where(c => c.ParentCategoryId is null)
                .Select(parent =>
                {
                    IReadOnlyList<SystemCategoryDto> children = childrenByParent.TryGetValue(parent.Id, out var kids)
                        ? [.. kids.Select(k => SystemCategoryDto.From(k, []))]
                        : [];
                    return SystemCategoryDto.From(parent, children);
                })
        ];

        return Ok(tree);
    }
}
