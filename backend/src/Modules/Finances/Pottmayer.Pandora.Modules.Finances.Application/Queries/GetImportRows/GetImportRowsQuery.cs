using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportRows;

public sealed record GetImportRowsInput(Guid UserId, Guid ImportFileId);

[RequiresPermission(FinancesPermissions.Imports.Read)]
public sealed class GetImportRowsQuery(GetImportRowsInput input)
    : QueryBase<GetImportRowsInput, IReadOnlyList<ImportRowDto>>(input);
