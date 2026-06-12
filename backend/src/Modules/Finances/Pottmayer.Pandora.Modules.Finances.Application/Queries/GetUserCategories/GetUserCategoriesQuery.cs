using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetUserCategories;

public sealed record GetUserCategoriesInput(Guid UserId, bool IncludeInactive);

public sealed class GetUserCategoriesQuery(GetUserCategoriesInput input)
    : QueryBase<GetUserCategoriesInput, IReadOnlyList<UserCategoryDto>>(input);
