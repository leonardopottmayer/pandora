namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>The union of property names and the direct pages of one folder, ready for a read-only table.</summary>
public sealed record FolderPropertiesDto(IReadOnlyList<string> Columns, IReadOnlyList<FolderPropertyPageDto> Pages);
