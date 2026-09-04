namespace Pottmayer.Pandora.Modules.Assistant.Application.Dtos;

/// <summary>
/// A user's assistant configuration for the settings screen. Holds no secret — the API key stays in
/// Integrations. When the user has no profile yet, the read returns sensible defaults with
/// <see cref="IsEnabled"/> false.
/// </summary>
public sealed record AssistantProfileDto(
    string Provider,
    string Model,
    bool IsEnabled,
    string? LocaleOverride,
    string ConfirmationLevel);
