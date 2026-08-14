namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// Nodes plus edges for the graph view. Every endpoint of an edge in <see cref="Edges"/> is
/// guaranteed to be in <see cref="Nodes"/>, so the frontend never has to draw an edge into nothing.
/// </summary>
public sealed record PageGraphDto(
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges);
