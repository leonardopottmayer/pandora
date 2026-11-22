namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// One <c>PageLink</c> edge. A page that both links and embeds another shows up as two edges, the
/// same way it shows up twice in the backlinks panel.
/// </summary>
public sealed record GraphEdgeDto(Guid SourceId, Guid TargetId, string Kind);
