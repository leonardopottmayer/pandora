namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>Generated plain text and the server's UTC completion time.</summary>
public sealed record AiGenerationDto(string Text, DateTimeOffset GeneratedAt);
