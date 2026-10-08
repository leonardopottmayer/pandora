using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.PreviewFilter;

public sealed record PreviewFilterInput(Guid UserId, FilterInput Filter);

public sealed class PreviewFilterQuery(PreviewFilterInput input)
    : QueryBase<PreviewFilterInput, FilterPreviewDto>(input);
