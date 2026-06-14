using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Localization.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetSystemCategories;

public sealed class GetSystemCategoriesQueryHandler(IUnitOfWorkFactory factory, IMessageProvider messages)
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

        // Display name is localized by code in the current culture; the seeded English name is the fallback.
        SystemCategoryDto Map(SystemCategory c, IReadOnlyList<SystemCategoryDto> children) =>
            new(c.Id, c.Code, messages.Get($"category.{c.Code}", c.Name), c.Nature.Value,
                c.Color, c.Icon, c.DisplayOrder, c.IsOther, c.IsActive, children);

        IReadOnlyList<SystemCategoryDto> tree =
        [
            .. all
                .Where(c => c.ParentCategoryId is null)
                .Select(parent =>
                {
                    IReadOnlyList<SystemCategoryDto> children = childrenByParent.TryGetValue(parent.Id, out var kids)
                        ? [.. kids.Select(k => Map(k, []))]
                        : [];
                    return Map(parent, children);
                })
        ];

        return Ok(tree);
    }
}
