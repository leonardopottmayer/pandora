using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportLayouts;

public sealed record GetImportLayoutsInput(Guid UserId);

[RequiresPermission(FinancesPermissions.Imports.Read)]
public sealed class GetImportLayoutsQuery(GetImportLayoutsInput input)
    : QueryBase<GetImportLayoutsInput, IReadOnlyList<ImportLayoutDto>>(input);
