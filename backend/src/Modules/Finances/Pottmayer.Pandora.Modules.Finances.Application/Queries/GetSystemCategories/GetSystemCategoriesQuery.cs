using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetSystemCategories;

public sealed record GetSystemCategoriesInput(string? Nature, bool IncludeInactive);

public sealed class GetSystemCategoriesQuery(GetSystemCategoriesInput input)
    : QueryBase<GetSystemCategoriesInput, IReadOnlyList<SystemCategoryDto>>(input);
