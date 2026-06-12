using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetUserCategories;

public sealed class GetUserCategoriesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetUserCategoriesQuery, IReadOnlyList<UserCategoryDto>>
{
    protected override async Task<Result<IReadOnlyList<UserCategoryDto>>> HandleAsync(
        GetUserCategoriesQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var all = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserCategoryRepository>();
            return await repo.GetAllForUserAsync(input.UserId, input.IncludeInactive, token);
        }, cancellationToken: ct);

        var childrenByParent = all
            .Where(c => c.ParentCategoryId is not null)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<UserCategory>)[.. g]);

        IReadOnlyList<UserCategoryDto> tree =
        [
            .. all
                .Where(c => c.ParentCategoryId is null)
                .Select(parent =>
                {
                    IReadOnlyList<UserCategoryDto> children = childrenByParent.TryGetValue(parent.Id, out var kids)
                        ? [.. kids.Select(k => UserCategoryDto.From(k))]
                        : [];
                    return UserCategoryDto.From(parent, children);
                })
        ];

        return Ok(tree);
    }
}
