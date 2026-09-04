namespace Pottmayer.Pandora.Modules.Assistant.Application.Dtos;

/// <summary>
/// A chat provider the assistant can use, and whether the user has an API key stored for it in
/// Integrations. <see cref="KeyHint"/> is a non-secret masked hint (e.g. the last four characters), or
/// null when no key is configured.
/// </summary>
public sealed record AssistantProviderDto(
    string Provider,
    string DisplayName,
    bool KeyConfigured,
    string? KeyHint);
