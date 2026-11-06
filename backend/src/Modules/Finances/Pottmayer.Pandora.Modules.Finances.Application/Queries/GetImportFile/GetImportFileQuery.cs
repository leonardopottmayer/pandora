using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportFile;

public sealed record GetImportFileInput(Guid UserId, Guid ImportFileId);

[RequiresPermission(FinancesPermissions.Imports.Read)]
public sealed class GetImportFileQuery(GetImportFileInput input)
    : QueryBase<GetImportFileInput, ImportFileDto>(input);
