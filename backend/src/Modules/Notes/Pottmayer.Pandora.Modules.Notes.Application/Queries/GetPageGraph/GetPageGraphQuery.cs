using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageGraph;

/// <summary>
/// <paramref name="RootPageId"/> null asks for the whole graph; set, it asks for the neighborhood of
/// that page within <paramref name="Depth"/> hops (the local graph). <paramref name="TagIds"/> cuts
/// the nodes down to the pages carrying all of them — before the neighborhood walk, so the depth is
/// counted over what is left.
/// </summary>
public sealed record GetPageGraphInput(
    Guid UserId, Guid? RootPageId, int Depth, IReadOnlyCollection<Guid>? TagIds = null);

public sealed class GetPageGraphQuery(GetPageGraphInput input)
    : QueryBase<GetPageGraphInput, PageGraphDto>(input);
