namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;

/// <summary>
/// A read-only view of a connected account, for consumers that need to know which accounts exist
/// (e.g. an account picker, or Agenda binding a calendar) without touching the module's schema.
/// <see cref="Label"/> is the user's name for it.
/// </summary>
public sealed record ExternalAccountSummary(
    Guid Id,
    Guid UserId,
    string Provider,
    string AuthKind,
    string ProviderAccountId,
    string? DisplayName,
    string Label,
    IReadOnlyList<string> Scopes,
    string Status);
