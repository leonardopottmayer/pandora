using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetRoots;

public sealed record GetRootsInput(Guid UserId);

public sealed class GetRootsQuery(GetRootsInput input)
    : QueryBase<GetRootsInput, IReadOnlyList<RootDto>>(input);
