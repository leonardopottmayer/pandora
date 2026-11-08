namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>A grounded answer and its consulted sources; null means insufficient information.</summary>
public sealed record NoteAnswerDto(string? Answer, IReadOnlyList<NoteSourceDto> Sources);
