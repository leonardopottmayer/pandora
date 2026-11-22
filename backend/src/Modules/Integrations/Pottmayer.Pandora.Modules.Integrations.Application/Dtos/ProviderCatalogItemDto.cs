namespace Pottmayer.Pandora.Modules.Integrations.Application.Dtos;

/// <summary>
/// An available provider and how many accounts the user has connected at it, for the settings catalog.
/// <see cref="AuthKind"/> tells the SPA how to add an account: <c>oauth</c> sends the browser to a
/// consent URL, <c>api-key</c> shows a field to paste a key, <c>password</c> a username + password form.
/// <see cref="DefaultScopes"/> is empty for the typed-in kinds.
/// </summary>
public sealed record ProviderCatalogItemDto(
    string Provider,
    string AuthKind,
    string? DisplayName,
    IReadOnlyList<string> DefaultScopes,
    int AccountCount);
