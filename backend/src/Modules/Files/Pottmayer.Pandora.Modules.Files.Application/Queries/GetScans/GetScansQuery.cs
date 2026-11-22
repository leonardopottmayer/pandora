using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetScans;

public sealed record GetScansInput(Guid UserId, Guid RootId);

[RequiresPermission(FilesPermissions.Scans.Read)]
public sealed class GetScansQuery(GetScansInput input)
    : QueryBase<GetScansInput, IReadOnlyList<ScanDto>>(input);
