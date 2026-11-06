using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.PreviewFilter;

public sealed record PreviewFilterInput(Guid UserId, FilterInput Filter);

[RequiresPermission(FilesPermissions.Filters.Read)]
public sealed class PreviewFilterQuery(PreviewFilterInput input)
    : QueryBase<PreviewFilterInput, FilterPreviewDto>(input);
