using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetFilters;

public sealed record GetFiltersInput(Guid UserId);

[RequiresPermission(FilesPermissions.Filters.Read)]
public sealed class GetFiltersQuery(GetFiltersInput input)
    : QueryBase<GetFiltersInput, IReadOnlyList<FilterDto>>(input);
