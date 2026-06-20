using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportRows;

public sealed record GetImportRowsInput(Guid UserId, Guid ImportFileId);

public sealed class GetImportRowsQuery(GetImportRowsInput input)
    : QueryBase<GetImportRowsInput, IReadOnlyList<ImportRowDto>>(input);
