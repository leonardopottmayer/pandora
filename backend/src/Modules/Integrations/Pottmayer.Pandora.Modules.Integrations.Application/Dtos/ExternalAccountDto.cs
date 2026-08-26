namespace Pottmayer.Pandora.Modules.Integrations.Application.Dtos;

/// <summary>A connected account as shown in settings. Never carries a token.</summary>
public sealed record ExternalAccountDto(
    Guid Id,
    string Provider,
    string AuthKind,
    string? DisplayName,
    IReadOnlyList<string> Scopes,
    string Status,
    string? LastError,
    DateTimeOffset ConnectedAt,
    DateTimeOffset? LastRefreshedAt);
