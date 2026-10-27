namespace Pottmayer.Pandora.Modules.Assistant.Application.Dtos;

/// <summary>
/// A chat provider the assistant can use, and how many API keys the user has stored for it in
/// Integrations. Which key the assistant uses is picked in the settings (the profile's chat account).
/// </summary>
public sealed record AssistantProviderDto(
    string Provider,
    string DisplayName,
    int KeyCount);
