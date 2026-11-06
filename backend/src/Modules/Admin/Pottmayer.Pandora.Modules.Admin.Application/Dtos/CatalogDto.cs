namespace Pottmayer.Pandora.Modules.Admin.Application.Dtos;

/// <summary>Every declared permission, grouped module → resource → action, for the roles matrix.</summary>
public sealed record CatalogModuleDto(string Key, IReadOnlyList<CatalogResourceDto> Resources);

/// <param name="Feature">The flag that gates the resource's permissions, if any.</param>
public sealed record CatalogResourceDto(string Key, string? Feature, IReadOnlyList<CatalogActionDto> Actions);

/// <param name="Key">The full permission key.</param>
/// <param name="Requires">Keys the permission depends on (its resource's <c>read</c>).</param>
public sealed record CatalogActionDto(string Key, string Action, IReadOnlyList<string> Requires);
