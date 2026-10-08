using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetScans;

public sealed record GetScansInput(Guid UserId, Guid RootId);

public sealed class GetScansQuery(GetScansInput input)
    : QueryBase<GetScansInput, IReadOnlyList<ScanDto>>(input);
