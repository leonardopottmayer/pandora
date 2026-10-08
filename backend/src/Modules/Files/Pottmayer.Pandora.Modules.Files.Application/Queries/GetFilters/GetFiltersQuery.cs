using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetFilters;

public sealed record GetFiltersInput(Guid UserId);

public sealed class GetFiltersQuery(GetFiltersInput input)
    : QueryBase<GetFiltersInput, IReadOnlyList<FilterDto>>(input);
