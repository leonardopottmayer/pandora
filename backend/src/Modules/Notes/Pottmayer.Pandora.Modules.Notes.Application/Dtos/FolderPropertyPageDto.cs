namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>A page summary and its plain-text property values, keyed by the response's canonical column names.</summary>
public sealed record FolderPropertyPageDto(PageSummaryDto Page, IReadOnlyDictionary<string, string> Properties);
