using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportFiles;

public sealed record GetImportFilesInput(Guid UserId, ImportFileFilter Filter);

[RequiresPermission(FinancesPermissions.Imports.Read)]
public sealed class GetImportFilesQuery(GetImportFilesInput input)
    : QueryBase<GetImportFilesInput, IReadOnlyList<ImportFileDto>>(input);
