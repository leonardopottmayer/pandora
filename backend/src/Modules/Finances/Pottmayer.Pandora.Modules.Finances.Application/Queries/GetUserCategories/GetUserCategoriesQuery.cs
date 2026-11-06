using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetUserCategories;

public sealed record GetUserCategoriesInput(Guid UserId, bool IncludeInactive);

[RequiresPermission(FinancesPermissions.Categories.Read)]
public sealed class GetUserCategoriesQuery(GetUserCategoriesInput input)
    : QueryBase<GetUserCategoriesInput, IReadOnlyList<UserCategoryDto>>(input);
