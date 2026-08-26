namespace Pottmayer.Pandora.Modules.Integrations.Application.Dtos;

/// <summary>An available provider and whether the user has connected it, for the settings catalog.</summary>
public sealed record ProviderCatalogItemDto(
    string Provider,
    IReadOnlyList<string> DefaultScopes,
    bool Connected,
    string? Status);
