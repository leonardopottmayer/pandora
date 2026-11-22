using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetRoots;

public sealed record GetRootsInput(Guid UserId);

[RequiresPermission(FilesPermissions.Roots.Read)]
public sealed class GetRootsQuery(GetRootsInput input)
    : QueryBase<GetRootsInput, IReadOnlyList<RootDto>>(input);
