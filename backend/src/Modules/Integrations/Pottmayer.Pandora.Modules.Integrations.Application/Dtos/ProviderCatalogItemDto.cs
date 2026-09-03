namespace Pottmayer.Pandora.Modules.Integrations.Application.Dtos;

/// <summary>
/// An available provider and whether the user has connected it, for the settings catalog.
/// <see cref="AuthKind"/> tells the SPA how to connect it: <c>oauth</c> sends the browser to a consent
/// URL, <c>api_key</c> shows a field to paste a key. <see cref="DefaultScopes"/> is empty for api_key.
/// </summary>
public sealed record ProviderCatalogItemDto(
    string Provider,
    string AuthKind,
    string? DisplayName,
    IReadOnlyList<string> DefaultScopes,
    bool Connected,
    string? Status);
