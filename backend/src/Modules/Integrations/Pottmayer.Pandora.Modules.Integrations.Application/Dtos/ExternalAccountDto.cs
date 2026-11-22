namespace Pottmayer.Pandora.Modules.Integrations.Application.Dtos;

/// <summary>
/// A connected account as shown in settings, with every place another module uses it. Never carries a
/// token.
/// </summary>
public sealed record ExternalAccountDto(
    Guid Id,
    string Provider,
    string AuthKind,
    string Label,
    string? DisplayName,
    IReadOnlyList<string> Scopes,
    string Status,
    string? LastError,
    DateTimeOffset ConnectedAt,
    DateTimeOffset? LastRefreshedAt,
    IReadOnlyList<AccountUsageDto> Usages);

/// <summary>One place an account is used: stable keys the screen translates, plus optional user data.</summary>
public sealed record AccountUsageDto(string Module, string Purpose, string? Detail);
