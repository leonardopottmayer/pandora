namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;

/// <summary>
/// A read-only view of a connected account, for consumers that need to know which accounts exist
/// (e.g. Agenda binding a calendar) without touching the module's schema.
/// </summary>
public sealed record ExternalAccountSummary(
    Guid Id,
    Guid UserId,
    string Provider,
    string AuthKind,
    string ProviderAccountId,
    string? DisplayName,
    IReadOnlyList<string> Scopes,
    string Status);
