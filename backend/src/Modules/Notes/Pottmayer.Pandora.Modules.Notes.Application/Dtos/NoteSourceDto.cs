namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>A page passage supplied to the model and cited by its answer.</summary>
public sealed record NoteSourceDto(Guid PageId, Guid SpaceId, string Title, string? Icon, string Excerpt);
